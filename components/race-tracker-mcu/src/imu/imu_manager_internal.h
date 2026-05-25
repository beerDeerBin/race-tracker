#pragma once

// Internal work variables for the IMU manager
typedef struct {
    uint32_t configuredRunId;
    uint32_t configuredNumberOfSamples;
    uint32_t currentSampleCount;
    float    xSensitivity;
    float    gSensitivity;
} ImuManagerWorkVar_t;

typedef struct {
    lsm6dsox_fifo_data_out_tag_t tag;
    int16_t                      x;
    int16_t                      y;
    int16_t                      z;
} __attribute__((packed)) FifoSample_t;

// Internal helpers
static float   IMUMGR_OdrToNumber(ImuManagerOdr_t odr);
static int32_t IMUMGR_AccelRangeToNumber(ImuManagerAccelRange_t accelRange);
static int32_t IMUMGR_GyroRangeToNumber(ImuManagerGyroRange_t gyroRange);

static float IMUMGR_AccelRawToMs2(int32_t mg);
static float IMUMGR_GyroRawToRads(int32_t mdps);

static void IMUMGR_Startup(void);
static void IMUMGR_Shutdown(void);
