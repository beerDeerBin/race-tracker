#pragma once

#include "../data/data_manager.h"
#include "../log/log_mod.h"

// IMU manager name
#define MODULE_IMUMGR "IMU_MGR"

// IMU manager error codes
#define IMUMGR_NO_ERROR     0x00
#define IMUMGR_INIT_ERROR   0x01
#define IMUMGR_CONFIG_ERROR (0x01 << 1)
#define IMUMGR_READ_ERROR   (0x01 << 2)
#define IMUMGR_FIFO_ERROR   (0x01 << 3)

// IMU manager configuration
#define IMUMGR_WIRE_CLOCK_HZ 400000

// IMU manager run configuration defaults
#define IMUMGR_DEFAULT_ODR_HZ         104
#define IMUMGR_DEFAULT_ACCEL_RANGE    4
#define IMUMGR_DEFAULT_GYRO_RANGE     500
#define IMUMGR_DEFAULT_FIFO_WATERMARK 128 // Number of samples in FIFO before reading, 128 ~ 64 records

typedef enum {
    IMUMGR_ODR_12_5Hz = 0x01,
    IMUMGR_ODR_26Hz   = 0x02,
    IMUMGR_ODR_52Hz   = 0x03,
    IMUMGR_ODR_104Hz  = 0x04,
    IMUMGR_ODR_208Hz  = 0x05,
    IMUMGR_ODR_417Hz  = 0x06,
    IMUMGR_ODR_833Hz  = 0x07,
} ImuManagerOdr_t;

typedef enum {
    IMUMGR_ACCEL_FS_2G  = 0x00,
    IMUMGR_ACCEL_FS_4G  = 0x02,
    IMUMGR_ACCEL_FS_8G  = 0x03,
    IMUMGR_ACCEL_FS_16G = 0x01,
} ImuManagerAccelRange_t;

typedef enum {
    IMUMGR_GYRO_FS_125DPS  = 0x01,
    IMUMGR_GYRO_FS_250DPS  = 0x00,
    IMUMGR_GYRO_FS_500DPS  = 0x02,
    IMUMGR_GYRO_FS_1000DPS = 0x04,
    IMUMGR_GYRO_FS_2000DPS = 0x06,
} ImuManagerGyroRange_t;

// Public function prototypes
void     IMUMGR_Init(void);
void     IMUMGR_ConfigureRun(uint32_t numberOfSamples, ImuManagerOdr_t odrHz,
                             ImuManagerAccelRange_t accelRangeG, ImuManagerGyroRange_t gyroRangeDps);
void     IMUMGR_StartRun(void);
void     IMUMGR_StopRun(void);
bool     IMUMGR_IsDataReady(void);
uint32_t IMUMGR_DrainFifo(void);
