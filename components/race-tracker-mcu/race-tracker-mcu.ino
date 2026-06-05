#include "src/index.h"
#include <Arduino.h>

// Main loop tuning
#define MAIN_STATUS_INTERVAL_MS 10000 // keepalive cadence while idle / connected
#define MAIN_PROGRESS_MARKS     10    // evenly-spaced progress updates per acquisition run
#define MAIN_LOOP_DELAY_MS      50    // idle pacing so the CPU can run minimal code between polls

// ---------------------------------------------------------------------------
//  Module-global state (RAM only — the device no longer deep sleeps)
// ---------------------------------------------------------------------------
static EepromData_t      eepromData;
static SystemState_t     sysState = SYS_STATE_IDLE;
static MqttCmdStartRun_t pendingRun;

static ErrorCode_t gFaultCode    = NO_ERROR; // sticky accumulated faults, reported in the health message
static uint32_t    gUptimeBaseMs = 0;        // millis() baseline; uptime = millis() - this (reset on RESET)
static uint32_t    gLastStatusMs = 0;        // last keepalive timestamp
static bool        gWasOnline    = false;    // tracks online edge so we publish once on (re)connect

// ---------------------------------------------------------------------------
//  Error reporting helpers
// ---------------------------------------------------------------------------

/**
 * @brief Accumulates a fallible operation's result into the sticky fault code.
 * @param code The ErrorCode_t returned by an operation.
 */
static void MAIN_Report(ErrorCode_t code)
{
    gFaultCode |= code;
}

/**
 * @brief Builds the error code reported in the health message: the sticky accumulated faults OR'd with any live
 * condition flags (currently a critical battery).
 * @return The combined ErrorCode_t bitmask.
 */
static ErrorCode_t MAIN_BuildErrorCode(void)
{
    ErrorCode_t code = gFaultCode;
    if (PWR_GetState() == PWR_STATE_CRITICAL_BATTERY)
    {
        code |= PWR_BATTERY_CRITICAL_ERROR;
    }
    return code;
}

// ---------------------------------------------------------------------------
//  Publishing helpers
// ---------------------------------------------------------------------------

/**
 * @brief Publishes a health/keepalive status message reflecting the current state, battery and error code.
 * @param sampledCount Samples collected so far in the current run (0 outside a run).
 * @param totalSamples Samples requested for the current run (0 outside a run).
 */
static void MAIN_PublishStatus(uint32_t sampledCount, uint32_t totalSamples)
{
    MqttStatus_t status;

    status.uptimeMs     = millis() - gUptimeBaseMs;
    status.batteryMv    = (uint16_t)PWR_GetBatteryMv();
    status.batteryPct   = PWR_GetBatteryPct();
    status.status       = (uint8_t)sysState;
    status.sampledCount = sampledCount;
    status.totalSamples = totalSamples;
    status.errorCode    = MAIN_BuildErrorCode();

    MAIN_Report(MQTT_PublishKeepalive(&status));
    gLastStatusMs = millis();

    gFaultCode = NO_ERROR; // clear the sticky fault code on each successful status publish
}

// ---------------------------------------------------------------------------
//  Connectivity
// ---------------------------------------------------------------------------

/**
 * @brief Ensures WiFi and the MQTT broker connection are up, reconnecting as needed. On the rising edge of becoming
 * fully online it enables modem sleep and publishes an initial status.
 * @return true if the device is fully online (WiFi + MQTT) after this call, false otherwise.
 */
static bool MAIN_EnsureOnline(void)
{
    if (!WIFI_IsConnected())
    {
        MAIN_Report(WIFI_Wakeup()); // set STA mode — WIFI_Init left the radio off (WIFI_OFF)
        WIFI_Connect();
    }
    if (WIFI_IsConnected() && !MQTT_IsConnected())
    {
        MQTT_Connect();
    }

    bool online = WIFI_IsConnected() && MQTT_IsConnected();
    if (online && !gWasOnline)
    {
        MAIN_Report(WIFI_EnableModemSleep());
        MAIN_PublishStatus(0, 0);
    }
    gWasOnline = online;
    return online;
}

// ---------------------------------------------------------------------------
//  State helpers
// ---------------------------------------------------------------------------

/**
 * @brief Validates that an acquisition run may start: battery must not be critical and the ring buffer must be empty.
 * @return true if a run may start, false otherwise.
 */
static bool MAIN_ValidateRun(void)
{
    if (PWR_GetState() == PWR_STATE_CRITICAL_BATTERY)
    {
        return false;
    }
    if (DAMGR_Count() > 0)
    {
        return false;
    }
    return true;
}

/**
 * @brief Handles a RESET command: zeroes the uptime baseline and accumulated faults, returns to IDLE and publishes a
 * fresh status. The GUID is not affected.
 */
static void MAIN_Reset(void)
{
    gUptimeBaseMs = millis();
    gFaultCode    = NO_ERROR;
    sysState      = SYS_STATE_IDLE;
    MAIN_PublishStatus(0, 0);
}

// ---------------------------------------------------------------------------
//  Acquisition
// ---------------------------------------------------------------------------

/**
 * @brief Drives a single acquisition run to completion: configures and starts the IMU, drains the FIFO while publishing
 * evenly-spaced progress updates, publishes the collected data in batches, then returns to the CONNECTED state. Blocks
 * until the run finishes. (CPU stays at 80 MHz — switching frequency at runtime breaks the live WiFi connection.)
 */
static void MAIN_RunAcquiring(void)
{
    uint32_t totalDrained = 0;
    uint32_t markIndex    = 1;
    uint32_t nextMark;

    MAIN_Report(WIFI_DisableModemSleep());

    MAIN_Report(IMUMGR_ConfigureRun(pendingRun.numSamples, (ImuManagerOdr_t)pendingRun.odr,
                                    (ImuManagerAccelRange_t)pendingRun.accelRange,
                                    (ImuManagerGyroRange_t)pendingRun.gyroRange));
    MAIN_Report(IMUMGR_StartRun());

    nextMark = (uint64_t)pendingRun.numSamples * markIndex / MAIN_PROGRESS_MARKS;

    while (totalDrained < pendingRun.numSamples)
    {
        while (!IMUMGR_IsDataReady())
        {
            delay(1);
        }
        totalDrained += IMUMGR_DrainFifo();

        while (markIndex <= MAIN_PROGRESS_MARKS && totalDrained >= nextMark)
        {
            MAIN_PublishStatus(totalDrained, pendingRun.numSamples);
            markIndex++;
            nextMark = (uint64_t)pendingRun.numSamples * markIndex / MAIN_PROGRESS_MARKS;
        }
    }

    MAIN_Report(IMUMGR_StopRun());

    uint32_t runOffset = 0;
    while (DAMGR_Count() > 0)
    {
        MAIN_Report(MQTT_PublishBatch(&pendingRun.runId, runOffset));
        runOffset += MQTT_MODULE_BATCH_MAX;
    }

    MAIN_Report(WIFI_EnableModemSleep());

    sysState = SYS_STATE_CONNECTED;
    MAIN_PublishStatus(totalDrained, pendingRun.numSamples);
}

// ---------------------------------------------------------------------------
//  State handlers
// ---------------------------------------------------------------------------

/**
 * @brief Handles a polled command while in the IDLE state.
 * @param cmd The command code returned by MQTT_PollCommand.
 */
static void MAIN_HandleIdle(uint8_t cmd)
{
    if (cmd == MQTT_CMD_CONNECT)
    {
        sysState = SYS_STATE_CONNECTED;
        MAIN_PublishStatus(0, 0);
    }
    else if (cmd == MQTT_CMD_RESET)
    {
        MAIN_Reset();
    }
}

/**
 * @brief Handles a polled command while in the CONNECTED state.
 * @param cmd The command code returned by MQTT_PollCommand.
 */
static void MAIN_HandleConnected(uint8_t cmd)
{
    if (cmd == MQTT_CMD_START_RUN)
    {
        if (MAIN_ValidateRun())
        {
            sysState = SYS_STATE_ACQUIRING;
        }
    }
    else if (cmd == MQTT_CMD_DISCONNECT)
    {
        sysState = SYS_STATE_IDLE;
        MAIN_PublishStatus(0, 0);
    }
    else if (cmd == MQTT_CMD_RESET)
    {
        MAIN_Reset();
    }
}

// ---------------------------------------------------------------------------
//  Arduino entry points
// ---------------------------------------------------------------------------

void setup()
{
    PWR_Init();
    PWR_SetCpuFreq(PWR_CPU_FREQ_IDLE_MHZ); // set the fixed frequency once, before WiFi comes up

    MAIN_Report(EEPROM_Init());
    MAIN_Report(DAMGR_Init());
    MAIN_Report(IMUMGR_Init());
    MAIN_Report(EEPROM_Read(&eepromData));
    MAIN_Report(WIFI_Init());
    MAIN_Report(MQTT_Init(&eepromData.guid));

    gUptimeBaseMs = millis();
    gLastStatusMs = millis();
}

void loop()
{
    PWR_Poll();

    if (!MAIN_EnsureOnline())
    {
        delay(MAIN_LOOP_DELAY_MS);
        return;
    }

    uint8_t cmd = MQTT_PollCommand(&pendingRun);

    switch (sysState)
    {
        case SYS_STATE_IDLE     : MAIN_HandleIdle(cmd); break;
        case SYS_STATE_CONNECTED: MAIN_HandleConnected(cmd); break;
        case SYS_STATE_ACQUIRING: MAIN_RunAcquiring(); break;
    }

    if (sysState != SYS_STATE_ACQUIRING && (millis() - gLastStatusMs) >= MAIN_STATUS_INTERVAL_MS)
    {
        MAIN_PublishStatus(0, 0);
    }

    delay(MAIN_LOOP_DELAY_MS);
}
