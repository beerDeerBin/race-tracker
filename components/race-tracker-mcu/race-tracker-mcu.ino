#include "../race-tracker-mcu/src/index.h"
#include <Arduino.h>

#define MAIN_CMD_POLL_TIMEOUT_MS 1000
#define MAIN_CMD_INTERVALL_MS    30000

static EepromData_t      eepromData;
static SystemState_t     sysState = SYS_STATE_IDLE;
static MqttCmdStartRun_t pendingRun;

// ---------------------------------------------------------------------------
//  State persistence helpers
// ---------------------------------------------------------------------------

static SystemState_t MAIN_GetStoredState(void) {
    return eepromData.sysState;
}

static void MAIN_SaveState(SystemState_t s) {
    eepromData.sysState = s;
    EEPROM_Write(&eepromData);
}

// ---------------------------------------------------------------------------
//  Shared helpers
// ---------------------------------------------------------------------------

static uint8_t MAIN_ConnectAndPoll(void) {
    uint32_t deadline;
    uint8_t  cmd;

    PWR_Poll();

    WIFI_Wakeup();

    if (!WIFI_Connect()) {
        MAIN_SleepCycle();
        return MQTT_CMD_NONE;
    }

    if (!MQTT_Connect()) {
        MAIN_SleepCycle();
        return MQTT_CMD_NONE;
    }

    MAIN_PublishStatus(0, 0);

    deadline = millis() + MAIN_CMD_POLL_TIMEOUT_MS;
    cmd      = MQTT_CMD_NONE;
    while (cmd == MQTT_CMD_NONE && millis() < deadline) {
        cmd = MQTT_PollCommand(&pendingRun);
        if (cmd == MQTT_CMD_NONE) delay(50);
    }

    return cmd;
}

static void MAIN_PublishStatus(uint32_t sampledCount, uint32_t totalSamples) {
    MqttStatus_t status;

    status.status       = (uint8_t)sysState;
    status.uptimeMs     = eepromData.uptimeMs + millis();
    status.batteryMv    = (uint16_t)PWR_GetBatteryMv();
    status.batteryPct   = PWR_GetBatteryPct();
    status.sampledCount = sampledCount;
    status.totalSamples = totalSamples;

    MQTT_PublishKeepalive(&status);
}

static void MAIN_SleepCycle(void) {
    if (MQTT_IsConnected()) MQTT_Disconnect();
    WIFI_Shutdown();
    eepromData.uptimeMs      += millis();
    eepromData.deepSleepFlag  = 1;
    MAIN_SaveState(sysState);
    PWR_DeepSleep(MAIN_CMD_INTERVALL_MS);
}

static void MAIN_Reset(void) {
    LOG_INFO("MAIN", 0, "reset: uptime cleared, -> IDLE");
    eepromData.uptimeMs = 0;
    sysState            = SYS_STATE_IDLE;
    MAIN_SaveState(SYS_STATE_IDLE);
    MAIN_PublishStatus(0, 0);
}

static bool MAIN_ValidateRun(void) {
    if (PWR_GetState() == PWR_STATE_CRITICAL_BATTERY) {
        LOG_WARNING("MAIN", 0, "run rejected: critical battery");
        return false;
    }
    if (DAMGR_Count() > 0) {
        LOG_WARNING("MAIN", 0, "run rejected: data buffer not empty (%lu records)", DAMGR_Count());
        return false;
    }
    return true;
}

// ---------------------------------------------------------------------------
//  State handlers
// ---------------------------------------------------------------------------

static void MAIN_RunIdle(void) {
    uint8_t cmd;

    cmd = MAIN_ConnectAndPoll();

    if (cmd == MQTT_CMD_CONNECT) {
        LOG_INFO("MAIN", 0, "-> CONNECTED");
        sysState = SYS_STATE_CONNECTED;
        MAIN_PublishStatus(0, 0);
    } else if (cmd == MQTT_CMD_RESET) {
        MAIN_Reset();
    } else if (cmd != MQTT_CMD_NONE) {
        LOG_WARNING("MAIN", 0, "command 0x%02X ignored in IDLE", cmd);
    }

    MAIN_SleepCycle();
}

static void MAIN_RunConnected(void) {
    uint8_t cmd;

    cmd = MAIN_ConnectAndPoll();

    if (cmd == MQTT_CMD_START_RUN) {
        if (MAIN_ValidateRun()) {
            LOG_INFO("MAIN", 0, "run validated, -> ACQUIRING");
            sysState = SYS_STATE_ACQUIRING;
            return; // keep WiFi/MQTT alive — go straight into acquiring
        }
        LOG_WARNING("MAIN", 0, "run rejected, staying CONNECTED");
    } else if (cmd == MQTT_CMD_DISCONNECT) {
        LOG_INFO("MAIN", 0, "-> IDLE");
        sysState = SYS_STATE_IDLE;
        MAIN_PublishStatus(0, 0);
    } else if (cmd == MQTT_CMD_RESET) {
        MAIN_Reset();
    } else if (cmd != MQTT_CMD_NONE) {
        LOG_WARNING("MAIN", 0, "command 0x%02X ignored in CONNECTED", cmd);
    }

    MAIN_SleepCycle();
}

static void MAIN_RunAcquiring(void) {
    uint32_t totalDrained;
    uint32_t lastStatusMs;
    uint32_t runOffset;

    totalDrained = 0;
    lastStatusMs = 0;
    runOffset    = 0;

    IMUMGR_ConfigureRun(pendingRun.numSamples, (ImuManagerOdr_t)pendingRun.odr,
                        (ImuManagerAccelRange_t)pendingRun.accelRange, (ImuManagerGyroRange_t)pendingRun.gyroRange);
    IMUMGR_StartRun();
    WIFI_EnableModemSleep();

    LOG_INFO("MAIN", 0, "acquiring: runId=%04X%04X..., samples=%lu", pendingRun.runId.data[0], pendingRun.runId.data[1],
             pendingRun.numSamples);

    while (totalDrained < pendingRun.numSamples) {
        while (!IMUMGR_IsDataReady()) { delay(1); }
        totalDrained += IMUMGR_DrainFifo();

        if (millis() - lastStatusMs >= 1000) {
            lastStatusMs = millis();
            WIFI_DisableModemSleep();
            MAIN_PublishStatus(totalDrained, pendingRun.numSamples);
            WIFI_EnableModemSleep();
        }
    }

    LOG_INFO("MAIN", 0, "run done: %lu samples, publishing batches", totalDrained);

    WIFI_DisableModemSleep();
    runOffset = 0;
    while (DAMGR_Count() > 0) {
        MQTT_PublishBatch(&pendingRun.runId, runOffset);
        runOffset += MQTT_MODULE_BATCH_MAX;
    }

    sysState = SYS_STATE_CONNECTED;
    MAIN_PublishStatus(totalDrained, pendingRun.numSamples);
    MAIN_SleepCycle();
}

// ---------------------------------------------------------------------------
//  Arduino entry points
// ---------------------------------------------------------------------------

void setup() {
    const char* stateStr;
    bool        validWakeup;

    LOG_Init();
    PWR_Init();
    EEPROM_Init();
    DAMGR_Init();
    IMUMGR_Init();
    EEPROM_Read(&eepromData);
    WIFI_Init();
    MQTT_Init(&eepromData.guid);

    validWakeup = eepromData.deepSleepFlag && PWR_WokeFromDeepSleep();
    if (!validWakeup) {
        LOG_INFO("MAIN", 0, "fresh boot — resetting state and uptime");
        eepromData.sysState = SYS_STATE_IDLE;
        eepromData.uptimeMs = 0;
    } else {
        eepromData.uptimeMs += MAIN_CMD_INTERVALL_MS;
    }

    eepromData.deepSleepFlag = 0;

    sysState = MAIN_GetStoredState();
    if (sysState == SYS_STATE_ACQUIRING) sysState = SYS_STATE_CONNECTED;

    stateStr = sysState == SYS_STATE_IDLE ? "IDLE" : "CONNECTED";
    LOG_INFO("MAIN", 0, "state: %s, uptime: %lu ms", stateStr, eepromData.uptimeMs);
}

void loop() {
    switch (sysState) {
        case SYS_STATE_IDLE     : MAIN_RunIdle(); break;
        case SYS_STATE_CONNECTED: MAIN_RunConnected(); break;
        case SYS_STATE_ACQUIRING: MAIN_RunAcquiring(); break;
    }
}