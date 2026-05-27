#pragma once

// Internal work variables for the IMU manager
typedef struct {
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

static float IMUMGR_AccelRawToMs2(float mg);
static float IMUMGR_GyroRawToRads(float mdps);

// LSM6DSOX FIFO tag sensor identifiers (from datasheet Table 79)
#define IMUMGR_FIFO_TAG_GYRO  0x01
#define IMUMGR_FIFO_TAG_ACCEL 0x02

static void IMUMGR_Startup(void);
static void IMUMGR_Shutdown(void);
