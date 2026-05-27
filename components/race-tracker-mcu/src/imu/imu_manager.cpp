// LSM6DSOXSensor must be included before internal.h (provides LSM6DSOXSensor types)
#include "LSM6DSOXSensor.h"

#include "imu_manager.h"
#include "imu_manager_internal.h"

#include <Wire.h>
#include <math.h>

// Work variables for the IMU manager
static ImuManagerWorkVar_t  imuManagerWorkVar;
static ImuManagerWorkVar_t* pImuManagerWorkVar;

// Driver object
static LSM6DSOXSensor imuManagerSox(&Wire, LSM6DSOX_I2C_ADD_L);

void IMUMGR_Init(void) {
    uint32_t status = (uint32_t)LSM6DSOX_OK;
    uint8_t  id;

    pImuManagerWorkVar = &imuManagerWorkVar;
    memset((void*)pImuManagerWorkVar, 0x00, sizeof(ImuManagerWorkVar_t));

    IMUMGR_Startup();

    LOG_INFO(MODULE_IMUMGR, IMUMGR_NO_ERROR, "LSM6DSOX detected, parked in power-down");

    IMUMGR_Shutdown();
}

void IMUMGR_ConfigureRun(uint32_t numberOfSamples, ImuManagerOdr_t odrHz,
                         ImuManagerAccelRange_t accelRangeG, ImuManagerGyroRange_t gyroRangeDps) {
    uint32_t status = (uint32_t)LSM6DSOX_OK;
    float    odrF   = IMUMGR_OdrToNumber(odrHz);

    IMUMGR_Startup();

    // Configure sensor ODR, FS, FIFO watermark level and get sensitivity values for later conversion of samples
    status |= (uint32_t)imuManagerSox.Set_X_ODR(odrF);
    status |= (uint32_t)imuManagerSox.Set_G_ODR(odrF);
    status |= (uint32_t)imuManagerSox.Set_FIFO_X_BDR(odrF);
    status |= (uint32_t)imuManagerSox.Set_FIFO_G_BDR(odrF);
    status |= (uint32_t)imuManagerSox.Set_X_FS(IMUMGR_AccelRangeToNumber(accelRangeG));
    status |= (uint32_t)imuManagerSox.Set_G_FS(IMUMGR_GyroRangeToNumber(gyroRangeDps));
    status |= (uint32_t)imuManagerSox.Set_FIFO_Watermark_Level(IMUMGR_DEFAULT_FIFO_WATERMARK);
    status |= imuManagerSox.Get_X_Sensitivity(&pImuManagerWorkVar->xSensitivity);
    status |= imuManagerSox.Get_G_Sensitivity(&pImuManagerWorkVar->gSensitivity);
    if (status != LSM6DSOX_OK) { LOG_ERROR(MODULE_IMUMGR, IMUMGR_CONFIG_ERROR, "failed to configure IMU"); }

    // Enable axes and set FIFO to bypass to flush any stale data
    status |= (uint32_t)imuManagerSox.Enable_X();
    status |= (uint32_t)imuManagerSox.Enable_G();
    status |= (uint32_t)imuManagerSox.Set_FIFO_Mode(LSM6DSOX_BYPASS_MODE);
    if (status != LSM6DSOX_OK) { LOG_ERROR(MODULE_IMUMGR, IMUMGR_CONFIG_ERROR, "failed to enable IMU axes"); }

    pImuManagerWorkVar->configuredNumberOfSamples = numberOfSamples;
    pImuManagerWorkVar->currentSampleCount        = 0;

    LOG_INFO(MODULE_IMUMGR, IMUMGR_NO_ERROR,
             "configured: Samples: %d, ODR: %.1fHz, Accel FS: ±%dg, Gyro FS: ±%ddps", numberOfSamples,
             odrF, IMUMGR_AccelRangeToNumber(accelRangeG), IMUMGR_GyroRangeToNumber(gyroRangeDps));
}

void IMUMGR_StartRun(void) {
    uint32_t status = (uint32_t)LSM6DSOX_OK;

    status |= (uint32_t)imuManagerSox.Set_FIFO_Mode(LSM6DSOX_STREAM_MODE);

    if (status != LSM6DSOX_OK) {
        LOG_ERROR(MODULE_IMUMGR, IMUMGR_FIFO_ERROR, "failed to wake up IMU");
        return;
    }

    LOG_INFO(MODULE_IMUMGR, IMUMGR_NO_ERROR, "started run");
}

void IMUMGR_StopRun(void) {
    uint32_t status = (uint32_t)LSM6DSOX_OK;

    status |= (uint32_t)imuManagerSox.Set_FIFO_Mode(LSM6DSOX_BYPASS_MODE);
    status |= (uint32_t)imuManagerSox.Disable_X();
    status |= (uint32_t)imuManagerSox.Disable_G();
    if (status != LSM6DSOX_OK) {
        LOG_ERROR(MODULE_IMUMGR, IMUMGR_FIFO_ERROR, "failed to power down IMU");
        return;
    }

    IMUMGR_Shutdown();

    LOG_INFO(MODULE_IMUMGR, IMUMGR_NO_ERROR, "stopped run");
}

bool IMUMGR_IsDataReady(void) {
    uint32_t status       = (uint32_t)LSM6DSOX_OK;
    uint16_t numOfSamples = 0;
    uint8_t  wtm          = 0;

    status  = imuManagerSox.Get_FIFO_Watermark_Status(&wtm);
    status |= imuManagerSox.Get_FIFO_Num_Samples(&numOfSamples);
    if (status != LSM6DSOX_OK) { LOG_ERROR(MODULE_IMUMGR, IMUMGR_READ_ERROR, "failed to read FIFO watermark status"); }

    if (wtm != 0) return true;

    return (pImuManagerWorkVar->configuredNumberOfSamples - pImuManagerWorkVar->currentSampleCount) <= numOfSamples;
}

uint32_t IMUMGR_DrainFifo(void) {
    uint32_t        status         = (uint32_t)LSM6DSOX_OK;
    uint16_t        fifoWords      = 0;
    uint32_t        drainedSamples = 0;
    uint32_t        samplesLeft;
    uint32_t        recordId;
    FifoSample_t    fifoData;
    SampleRecord_t* pRecord;
    bool            accelRead = false;
    bool            gyroRead  = false;
    float           ax = 0, ay = 0, az = 0;
    float           gx = 0, gy = 0, gz = 0;

    status = imuManagerSox.Get_FIFO_Num_Samples(&fifoWords);
    if (status != LSM6DSOX_OK) {
        LOG_ERROR(MODULE_IMUMGR, IMUMGR_READ_ERROR, "failed to read FIFO sample count");
        return drainedSamples;
    }

    fifoWords   = (uint16_t)min((uint32_t)fifoWords, (uint32_t)IMUMGR_DEFAULT_FIFO_WATERMARK);
    samplesLeft = pImuManagerWorkVar->configuredNumberOfSamples - pImuManagerWorkVar->currentSampleCount;

    for (uint16_t w = 0; w < fifoWords && drainedSamples < samplesLeft; w++) {
        status = (uint32_t)imuManagerSox.Get_FIFO_Sample((uint8_t*)&fifoData, 1);
        if (status != LSM6DSOX_OK) {
            LOG_ERROR(MODULE_IMUMGR, IMUMGR_READ_ERROR, "failed to read FIFO sample");
            return drainedSamples;
        }

        if (fifoData.tag.tag_sensor == IMUMGR_FIFO_TAG_GYRO) {
            gx       = (float)fifoData.x * pImuManagerWorkVar->gSensitivity;
            gy       = (float)fifoData.y * pImuManagerWorkVar->gSensitivity;
            gz       = (float)fifoData.z * pImuManagerWorkVar->gSensitivity;
            gyroRead = true;
        } else if (fifoData.tag.tag_sensor == IMUMGR_FIFO_TAG_ACCEL) {
            ax        = (float)fifoData.x * pImuManagerWorkVar->xSensitivity;
            ay        = (float)fifoData.y * pImuManagerWorkVar->xSensitivity;
            az        = (float)fifoData.z * pImuManagerWorkVar->xSensitivity;
            accelRead = true;
        } else {
            continue;
        }

        if (!accelRead || !gyroRead) { continue; }

        DAMGR_Reserve(&pRecord, &recordId);
        if (pRecord == NULL) {
            LOG_ERROR(MODULE_IMUMGR, IMUMGR_FIFO_ERROR, "data manager queue full, cannot reserve record");
            return drainedSamples;
        }
        pRecord->ax     = IMUMGR_AccelRawToMs2(ax);
        pRecord->ay     = IMUMGR_AccelRawToMs2(ay);
        pRecord->az     = IMUMGR_AccelRawToMs2(az);
        pRecord->gx     = IMUMGR_GyroRawToRads(gx);
        pRecord->gy     = IMUMGR_GyroRawToRads(gy);
        pRecord->gz     = IMUMGR_GyroRawToRads(gz);
        DAMGR_Push(recordId);
        pImuManagerWorkVar->currentSampleCount++;
        drainedSamples++;
        accelRead = false;
        gyroRead  = false;
    }

    return drainedSamples;
}

static void IMUMGR_Startup(void) {
    uint32_t status = (uint32_t)LSM6DSOX_OK;
    uint8_t  id;

    Wire.begin();
    Wire.setClock(IMUMGR_WIRE_CLOCK_HZ);

    status |= (uint32_t)imuManagerSox.begin();
    status |= (uint32_t)imuManagerSox.ReadID(&id);

    if (status != LSM6DSOX_OK || id != LSM6DSOX_ID) {
        LOG_ERROR(MODULE_IMUMGR, IMUMGR_INIT_ERROR, "failed to initialize IMU driver");
    }
}

static void IMUMGR_Shutdown(void) {
    uint32_t status = (uint32_t)LSM6DSOX_OK;

    status |= (uint32_t)imuManagerSox.end();
    Wire.end();

    if (status != LSM6DSOX_OK) {
        LOG_ERROR(MODULE_IMUMGR, IMUMGR_INIT_ERROR, "failed to shut down IMU");
        return;
    }
}

static float IMUMGR_OdrToNumber(ImuManagerOdr_t odr) {
    if (odr == IMUMGR_ODR_12_5Hz) { return 12.5f; }
    if (odr == IMUMGR_ODR_26Hz) { return 26.0f; }
    if (odr == IMUMGR_ODR_52Hz) { return 52.0f; }
    if (odr == IMUMGR_ODR_104Hz) { return 104.0f; }
    if (odr == IMUMGR_ODR_208Hz) { return 208.0f; }
    if (odr == IMUMGR_ODR_417Hz) { return 417.0f; }
    if (odr == IMUMGR_ODR_833Hz) { return 833.0f; }
    return 0.0f;
}

static int32_t IMUMGR_AccelRangeToNumber(ImuManagerAccelRange_t accelRange) {
    if (accelRange == IMUMGR_ACCEL_FS_2G) { return 2; }
    if (accelRange == IMUMGR_ACCEL_FS_4G) { return 4; }
    if (accelRange == IMUMGR_ACCEL_FS_8G) { return 8; }
    if (accelRange == IMUMGR_ACCEL_FS_16G) { return 16; }
    return 0;
}

static int32_t IMUMGR_GyroRangeToNumber(ImuManagerGyroRange_t gyroRange) {
    if (gyroRange == IMUMGR_GYRO_FS_125DPS) { return 125; }
    if (gyroRange == IMUMGR_GYRO_FS_250DPS) { return 250; }
    if (gyroRange == IMUMGR_GYRO_FS_500DPS) { return 500; }
    if (gyroRange == IMUMGR_GYRO_FS_1000DPS) { return 1000; }
    if (gyroRange == IMUMGR_GYRO_FS_2000DPS) { return 2000; }
    return 0;
}

static float IMUMGR_AccelRawToMs2(float mg) {
    return mg * 0.001f * 9.80665f;
}

static float IMUMGR_GyroRawToRads(float mdps) {
    return mdps * 0.001f * ((float)M_PI / 180.0f);
}
