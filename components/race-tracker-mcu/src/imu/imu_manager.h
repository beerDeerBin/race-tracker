#pragma once

#include "../config.h"
#include "../data/data_manager.h"

// IMU manager configuration
#define IMUMGR_WIRE_CLOCK_HZ 400000

// IMU manager run configuration defaults
#define IMUMGR_DEFAULT_ODR_HZ         104
#define IMUMGR_DEFAULT_ACCEL_RANGE    4
#define IMUMGR_DEFAULT_GYRO_RANGE     500
#define IMUMGR_DEFAULT_FIFO_WATERMARK 128 // Number of samples in FIFO before reading, 128 ~ 64 records

typedef enum
{
    IMUMGR_ODR_12_5Hz = 0x01,
    IMUMGR_ODR_26Hz   = 0x02,
    IMUMGR_ODR_52Hz   = 0x03,
    IMUMGR_ODR_104Hz  = 0x04,
    IMUMGR_ODR_208Hz  = 0x05,
    IMUMGR_ODR_417Hz  = 0x06,
    IMUMGR_ODR_833Hz  = 0x07,
} ImuManagerOdr_t;

typedef enum
{
    IMUMGR_ACCEL_FS_2G  = 0x00,
    IMUMGR_ACCEL_FS_4G  = 0x02,
    IMUMGR_ACCEL_FS_8G  = 0x03,
    IMUMGR_ACCEL_FS_16G = 0x01,
} ImuManagerAccelRange_t;

typedef enum
{
    IMUMGR_GYRO_FS_125DPS  = 0x01,
    IMUMGR_GYRO_FS_250DPS  = 0x00,
    IMUMGR_GYRO_FS_500DPS  = 0x02,
    IMUMGR_GYRO_FS_1000DPS = 0x04,
    IMUMGR_GYRO_FS_2000DPS = 0x06,
} ImuManagerGyroRange_t;

// Public function prototypes
ErrorCode_t IMUMGR_Init(void);
ErrorCode_t IMUMGR_ConfigureRun(uint32_t numberOfSamples, ImuManagerOdr_t odrHz, ImuManagerAccelRange_t accelRangeG,
                                ImuManagerGyroRange_t gyroRangeDps);
ErrorCode_t IMUMGR_StartRun(void);
ErrorCode_t IMUMGR_StopRun(void);
bool        IMUMGR_IsDataReady(void);
uint32_t    IMUMGR_DrainFifo(void);
