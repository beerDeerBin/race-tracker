#include "../race-tracker-mcu/src/index.h"
#include <Arduino.h>

#define TEST_RUN_ID      1
#define TEST_NUM_SAMPLES 8330 // 10 seconds of data at 833Hz

static SampleRecord_t buffer[10];
static uint32_t       runNumber = 0;

static const uint32_t odrToNumOfSamples[] = {125, 260, 520, 1040, 2080, 4170, 8330};

void setup() {
    LOG_Init();
    EEPROM_Init();
    DAMGR_Init();
    IMUMGR_Init();
}

void loop() {
    simulateOneRun();
    runNumber++;
    delay(1000 + millis() % 10);
}

void simulateOneRun() {
    uint32_t startTs      = millis();
    uint32_t drained      = 0;
    uint32_t popped       = 0;
    uint32_t totalDrained = 0;
    uint32_t duration     = 0;
    uint32_t cnt          = 0;

    uint32_t odr          = micros() % 7 + 1; // Random ODR between 12.5Hz and 833Hz
    uint32_t numOfSamples = odrToNumOfSamples[odr - 1];
    LOG_INFO("MAIN", 0, "Starting run %d with ODR: %d Hz", runNumber, numOfSamples / 10);

    bool runDone = false;
    IMUMGR_ConfigureRun(runNumber, numOfSamples, (ImuManagerOdr_t)odr, IMUMGR_ACCEL_FS_4G, IMUMGR_GYRO_FS_500DPS);
    IMUMGR_StartRun();

    while (!runDone) {
        while (!IMUMGR_IsDataReady()) { delay(1); }
        drained       = IMUMGR_DrainFifo();
        totalDrained += drained;

        if (totalDrained >= numOfSamples) {
            runDone  = true;
            duration = millis() - startTs;

            while (DAMGR_Count() > 0) {
                DAMGR_Pop(buffer, 5);
                popped += 5;
            }
        }
    }

    LOG_INFO("MAIN", 0, "Run %d done. Duration: %d ms, Drained: %d, Popped: %d\n", runNumber, duration, totalDrained,
             popped);
}