#include "../race-tracker-mcu/src/index.h"
#include <Arduino.h>

#define TEST_RUN_ID      1
#define TEST_NUM_SAMPLES 8330 // 10 seconds of data at 833Hz

static SampleRecord_t buffer[10];
static uint32_t       runNumber = 0;

void setup() {
    LOG_Init();
    EEPROM_Init();
    DAMGR_Init();
    IMUMGR_Init();
}

void loop() {
    simulateOneRun();
    runNumber++;
    delay(1000);
}

void simulateOneRun() {
    uint32_t startTs      = millis();
    uint32_t drained      = 0;
    uint32_t popped       = 0;
    uint32_t totalDrained = 0;
    uint32_t duration     = 0;
    uint32_t cnt          = 0;

    bool runDone = false;
    IMUMGR_ConfigureRun(runNumber, TEST_NUM_SAMPLES, IMUMGR_ODR_833Hz, IMUMGR_ACCEL_FS_4G, IMUMGR_GYRO_FS_500DPS);
    IMUMGR_StartRun();

    while (!runDone) {
        while (!IMUMGR_IsWatermarkReached()) { delay(1); }
        drained       = IMUMGR_DrainFifo();
        totalDrained += drained;

        if (totalDrained >= TEST_NUM_SAMPLES) {
            runDone  = true;
            duration = millis() - startTs;

            while (DAMGR_Count() > 0) {
                DAMGR_Pop(buffer, 10);
                popped += 10;
            }
        }
    }

    LOG_INFO("MAIN", 0, "Run %d done. Duration: %d ms, Drained: %d, Popped: %d\n", runNumber, duration, totalDrained,
             popped);
}