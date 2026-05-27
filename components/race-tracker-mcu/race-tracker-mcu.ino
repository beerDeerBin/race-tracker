#include "../race-tracker-mcu/src/index.h"
#include <Arduino.h>

#define TEST_RUN_ID      1
#define TEST_NUM_SAMPLES 8330 // 10 seconds of data at 833Hz

#define EEPROM_STATUS_PWR_TEST_PENDING (1 << 0)

static uint32_t     runNumber = 0;
static uint32_t     runOffset = 0;
static EepromData_t eepromData;

static const float odrToNumOfSamples[] = {12.5, 26, 52, 104, 208, 417, 833};

void setup() {
    LOG_Init();
    PWR_Init();
    EEPROM_Init();
    DAMGR_Init();
    IMUMGR_Init();
    EEPROM_Read(&eepromData);
    WIFI_Init();
    MQTT_Init(&eepromData.guid);

    testPower();
}

void loop() {
    // simulateOneRun();
    // runNumber++;
    // runOffset = 0;
    // delay(1000 + millis() % 10);

    PWR_Poll();

    uint8_t  cmd      = MQTT_CMD_NONE;
    uint32_t deadline = 0;

    WIFI_Wakeup();

    if (WIFI_Connect()) {
        Serial.println("WIFI connect");
        if (MQTT_Connect()) {

            deadline = millis() + 1000; // 3 seconds

            while (cmd == MQTT_CMD_NONE && millis() < deadline) {

                MqttStatus_t status = {millis(), 0, (uint16_t)PWR_GetBatteryMv(), PWR_GetBatteryPct()};
                MQTT_PublishKeepalive(&status);

                cmd = MQTT_PollCommand(nullptr);

                if (cmd != MQTT_CMD_NONE) { Serial.printf("Got command: %d\n", cmd); }

                delay(1000);
            }

            MQTT_Disconnect();

            WIFI_Shutdown();

            delay(5000);
        }
    }

    delay(1000 + millis() % 10);
}

void testPower() {
    LOG_INFO("MAIN", 0, "--- power test begin ---");

    // Battery readings
    float   mv  = PWR_GetBatteryMv();
    uint8_t pct = PWR_GetBatteryPct();
    LOG_INFO("MAIN", 0, "battery: %.0f mV, %u%%", mv, pct);

    // Power state
    PwrState_t  state    = PWR_GetState();
    const char* stateStr = (state == PWR_STATE_NORMAL)             ? "NORMAL"
                           : (state == PWR_STATE_LOW_BATTERY)      ? "LOW"
                           : (state == PWR_STATE_CRITICAL_BATTERY) ? "CRITICAL"
                                                                   : "UNKNOWN";
    LOG_INFO("MAIN", 0, "state: %s", stateStr);

    // CPU frequency scaling
    LOG_INFO("MAIN", 0, "scaling CPU to idle (%u MHz)...", PWR_CPU_FREQ_IDLE_MHZ);
    PWR_SetCpuFreq(PWR_CPU_FREQ_IDLE_MHZ);
    delay(200);
    LOG_INFO("MAIN", 0, "scaling CPU to active (%u MHz)...", PWR_CPU_FREQ_ACTIVE_MHZ);
    PWR_SetCpuFreq(PWR_CPU_FREQ_ACTIVE_MHZ);

    // Light sleep — 500 ms, should resume here automatically
    LOG_INFO("MAIN", 0, "light sleep 500 ms...");
    PWR_LightSleep(500);
    LOG_INFO("MAIN", 0, "woke from light sleep");

    // Poll — forces a battery check regardless of interval
    LOG_INFO("MAIN", 0, "manual poll...");
    PWR_Poll();

    // Deep sleep round-trip test — flag persists across reset in EEPROM
    if (eepromData.status & EEPROM_STATUS_PWR_TEST_PENDING) {
        LOG_INFO("MAIN", 0, "deep sleep round-trip: OK");
        eepromData.status &= ~EEPROM_STATUS_PWR_TEST_PENDING;
        EEPROM_Write(&eepromData);
    } else {
        LOG_INFO("MAIN", 0, "deep sleep 5000 ms...");
        eepromData.status |= EEPROM_STATUS_PWR_TEST_PENDING;
        EEPROM_Write(&eepromData);
        PWR_DeepSleep(5000);
    }

    LOG_INFO("MAIN", 0, "--- power test end ---");
}

void testWifi() {
    LOG_INFO("MAIN", 0, "--- WiFi test begin ---");

    bool connected = WIFI_Connect();
    if (connected) {
        LOG_INFO("MAIN", 0, "modem sleep test...");
        WIFI_EnableModemSleep();
        delay(500);
        WIFI_DisableModemSleep();
        LOG_INFO("MAIN", 0, "isConnected after modem sleep cycle: %s", WIFI_IsConnected() ? "true" : "false");

        LOG_INFO("MAIN", 0, "shutdown/wakeup/reconnect test...");
        WIFI_Shutdown();
        LOG_INFO("MAIN", 0, "isConnected after shutdown: %s", WIFI_IsConnected() ? "true" : "false");
        delay(500);
        WIFI_Wakeup();
        connected = WIFI_Connect();
        LOG_INFO("MAIN", 0, "isConnected after wakeup+connect: %s", connected ? "true" : "false");
        WIFI_Shutdown();
    } else {
        LOG_INFO("MAIN", 0, "WiFi not connected, skipping tests");
    }

    LOG_INFO("MAIN", 0, "--- WiFi test end ---");
}

void testMqtt() {
    LOG_INFO("MAIN", 0, "--- MQTT test begin ---");

    bool wifiOk = WIFI_Connect();
    if (!wifiOk) {
        LOG_INFO("MAIN", 0, "WiFi failed, skipping MQTT test");
        return;
    }

    bool mqttOk = MQTT_Connect();
    if (mqttOk) {
        MqttStatus_t status = {millis(), 0};
        MQTT_PublishKeepalive(&status);

        LOG_INFO("MAIN", 0, "polling for commands for 3s...");
        uint32_t          deadline = millis() + 3000;
        MqttCmdStartRun_t cmd;
        while (millis() < deadline) {
            uint8_t cmdType = MQTT_PollCommand(&cmd);
            if (cmdType == MQTT_CMD_CONNECT) {
                LOG_INFO("MAIN", 0, "got CONNECT command from FE");
            } else if (cmdType == MQTT_CMD_START_RUN) {
                LOG_INFO("MAIN", 0, "got START_RUN: runId=%lu, samples=%lu, odr=%d", cmd.runId, cmd.numSamples,
                         cmd.odr);
            }
            delay(50);
        }

        MQTT_Disconnect();
    }

    WIFI_Shutdown();
    LOG_INFO("MAIN", 0, "--- MQTT test end ---");
}

void simulateOneRun() {
    uint32_t startTs      = millis();
    uint32_t drained      = 0;
    uint32_t totalDrained = 0;
    uint32_t duration     = 0;

    uint32_t runtime = 10;
    uint32_t odr     = 6;
    // uint32_t odr          = micros() % 7 + 1;
    uint32_t numOfSamples = (uint32_t)odrToNumOfSamples[odr - 1] * runtime;
    LOG_INFO("MAIN", 0, "starting run %lu with %lu samples", runNumber, numOfSamples);

    runOffset = 0;
    IMUMGR_ConfigureRun(numOfSamples, (ImuManagerOdr_t)odr, IMUMGR_ACCEL_FS_4G, IMUMGR_GYRO_FS_500DPS);
    IMUMGR_StartRun();

    bool runDone = false;
    while (!runDone) {
        while (!IMUMGR_IsDataReady()) { delay(1); }
        drained       = IMUMGR_DrainFifo();
        totalDrained += drained;

        if (totalDrained >= numOfSamples) {
            runDone  = true;
            duration = millis() - startTs;

            Serial.println("Run done");

            WIFI_Wakeup();

            if (WIFI_Connect()) {
                Serial.println("WIFI connect");
                if (MQTT_Connect()) {
                    Serial.println("MQTT done");
                    MqttStatus_t status = {millis(), 0, (uint16_t)PWR_GetBatteryMv(), PWR_GetBatteryPct()};
                    MQTT_PublishKeepalive(&status);

                    while (DAMGR_Count() >= 0) {
                        MQTT_PublishBatch(runNumber, runOffset);
                        runOffset += MQTT_MODULE_BATCH_MAX;
                    }

                    MQTT_Disconnect();

                    WIFI_Shutdown();
                }
            }
        }
    }

    LOG_INFO("MAIN", 0, "run %lu done: %lu ms, %lu samples", runNumber, duration, totalDrained);
}