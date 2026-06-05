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

/**
 * @brief Initializes the IMU manager by bringing the sensor up to verify it responds, then powering it back down to a
 * known idle state. Must be called once at startup before any other IMU function.
 * @return ErrorCode_t indicating the success or failure of the initialization process.
 */
ErrorCode_t IMUMGR_Init(void)
{
    ErrorCode_t errorCode = NO_ERROR;

    pImuManagerWorkVar = &imuManagerWorkVar;
    memset((void*)pImuManagerWorkVar, 0x00, sizeof(ImuManagerWorkVar_t));

    errorCode |= IMUMGR_Startup();
    errorCode |= IMUMGR_Shutdown();

    return errorCode;
}

/**
 * @brief Configures the sensor for a measurement run: powers it up, sets the output data rate, full-scale ranges and
 * FIFO watermark, caches the sensitivity values used for conversion, enables both axes and flushes the FIFO. Call
 * IMUMGR_StartRun afterwards to begin streaming samples.
 * @param numberOfSamples Total number of sample records to acquire during the run.
 * @param odrHz Output data rate selector.
 * @param accelRangeG Accelerometer full-scale range selector.
 * @param gyroRangeDps Gyroscope full-scale range selector.
 * @return ErrorCode_t indicating the success or failure of the configuration.
 */
ErrorCode_t IMUMGR_ConfigureRun(uint32_t numberOfSamples, ImuManagerOdr_t odrHz, ImuManagerAccelRange_t accelRangeG,
                                ImuManagerGyroRange_t gyroRangeDps)
{
    ErrorCode_t errorCode = NO_ERROR;
    uint32_t    status    = (uint32_t)LSM6DSOX_OK;
    float       odrF      = IMUMGR_OdrToNumber(odrHz);

    errorCode |= IMUMGR_Startup();

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

    // Enable axes and set FIFO to bypass to flush any stale data
    status |= (uint32_t)imuManagerSox.Enable_X();
    status |= (uint32_t)imuManagerSox.Enable_G();
    status |= (uint32_t)imuManagerSox.Set_FIFO_Mode(LSM6DSOX_BYPASS_MODE);
    if (status != LSM6DSOX_OK)
    {
        errorCode |= IMU_CONFIG_ERROR;
    }

    pImuManagerWorkVar->configuredNumberOfSamples = numberOfSamples;
    pImuManagerWorkVar->currentSampleCount        = 0;

    return errorCode;
}

/**
 * @brief Starts a configured run by switching the FIFO into stream mode so samples begin accumulating.
 * @return ErrorCode_t indicating the success or failure of the operation.
 */
ErrorCode_t IMUMGR_StartRun(void)
{
    if (imuManagerSox.Set_FIFO_Mode(LSM6DSOX_STREAM_MODE) != LSM6DSOX_OK)
    {
        return IMU_FIFO_ERROR;
    }

    return NO_ERROR;
}

/**
 * @brief Stops the current run by flushing the FIFO, disabling both axes and powering the sensor back down.
 * @return ErrorCode_t indicating the success or failure of the operation.
 */
ErrorCode_t IMUMGR_StopRun(void)
{
    ErrorCode_t errorCode = NO_ERROR;
    uint32_t    status    = (uint32_t)LSM6DSOX_OK;

    status |= (uint32_t)imuManagerSox.Set_FIFO_Mode(LSM6DSOX_BYPASS_MODE);
    status |= (uint32_t)imuManagerSox.Disable_X();
    status |= (uint32_t)imuManagerSox.Disable_G();
    if (status != LSM6DSOX_OK)
    {
        return IMU_FIFO_ERROR;
    }

    errorCode |= IMUMGR_Shutdown();

    return errorCode;
}

/**
 * @brief Checks whether enough samples are buffered in the FIFO to warrant draining, either because the watermark has
 * been reached or because the remaining samples of the run already fit in the FIFO.
 * @return true if data is ready to be drained, false otherwise.
 */
bool IMUMGR_IsDataReady(void)
{
    uint32_t status       = (uint32_t)LSM6DSOX_OK;
    uint16_t numOfSamples = 0;
    uint8_t  wtm          = 0;

    status  = imuManagerSox.Get_FIFO_Watermark_Status(&wtm);
    status |= imuManagerSox.Get_FIFO_Num_Samples(&numOfSamples);

    if (wtm != 0)
    {
        return true;
    }

    return (pImuManagerWorkVar->configuredNumberOfSamples - pImuManagerWorkVar->currentSampleCount) <= numOfSamples;
}

/**
 * @brief Drains buffered FIFO words, pairing each accelerometer and gyroscope reading into a SampleRecord_t (converted
 * to SI units) and committing it to the data manager. Stops early once the run's remaining sample budget is reached, a
 * FIFO read fails, or the data manager has no free slot.
 * @return The number of complete sample records drained and committed during this call.
 */
uint32_t IMUMGR_DrainFifo(void)
{
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
    if (status != LSM6DSOX_OK)
    {
        return drainedSamples;
    }

    fifoWords   = (uint16_t)min((uint32_t)fifoWords, (uint32_t)IMUMGR_DEFAULT_FIFO_WATERMARK);
    samplesLeft = pImuManagerWorkVar->configuredNumberOfSamples - pImuManagerWorkVar->currentSampleCount;

    for (uint16_t w = 0; w < fifoWords && drainedSamples < samplesLeft; w++)
    {
        status = (uint32_t)imuManagerSox.Get_FIFO_Sample((uint8_t*)&fifoData, 1);
        if (status != LSM6DSOX_OK)
        {
            return drainedSamples;
        }

        if (fifoData.tag.tag_sensor == IMUMGR_FIFO_TAG_GYRO)
        {
            gx       = (float)fifoData.x * pImuManagerWorkVar->gSensitivity;
            gy       = (float)fifoData.y * pImuManagerWorkVar->gSensitivity;
            gz       = (float)fifoData.z * pImuManagerWorkVar->gSensitivity;
            gyroRead = true;
        }
        else if (fifoData.tag.tag_sensor == IMUMGR_FIFO_TAG_ACCEL)
        {
            ax        = (float)fifoData.x * pImuManagerWorkVar->xSensitivity;
            ay        = (float)fifoData.y * pImuManagerWorkVar->xSensitivity;
            az        = (float)fifoData.z * pImuManagerWorkVar->xSensitivity;
            accelRead = true;
        }
        else
        {
            continue;
        }

        if (!accelRead || !gyroRead)
        {
            continue;
        }

        DAMGR_Reserve(&pRecord, &recordId);
        if (pRecord == NULL)
        {
            return drainedSamples;
        }
        pRecord->ax = IMUMGR_AccelRawToMs2(ax);
        pRecord->ay = IMUMGR_AccelRawToMs2(ay);
        pRecord->az = IMUMGR_AccelRawToMs2(az);
        pRecord->gx = IMUMGR_GyroRawToRads(gx);
        pRecord->gy = IMUMGR_GyroRawToRads(gy);
        pRecord->gz = IMUMGR_GyroRawToRads(gz);
        DAMGR_Push(recordId);
        pImuManagerWorkVar->currentSampleCount++;
        drainedSamples++;
        accelRead = false;
        gyroRead  = false;
    }

    return drainedSamples;
}

/**
 * @brief Brings the sensor up: starts the I2C bus, begins the driver and verifies the device id.
 * @return ErrorCode_t indicating the success or failure of the startup.
 */
static ErrorCode_t IMUMGR_Startup(void)
{
    uint32_t status = (uint32_t)LSM6DSOX_OK;
    uint8_t  id;

    Wire.begin();
    Wire.setClock(IMUMGR_WIRE_CLOCK_HZ);

    status |= (uint32_t)imuManagerSox.begin();
    status |= (uint32_t)imuManagerSox.ReadID(&id);

    if (status != LSM6DSOX_OK || id != LSM6DSOX_ID)
    {
        return IMU_INIT_ERROR;
    }

    return NO_ERROR;
}

/**
 * @brief Powers the sensor down and releases the I2C bus.
 * @return ErrorCode_t indicating the success or failure of the shutdown.
 */
static ErrorCode_t IMUMGR_Shutdown(void)
{
    uint32_t status = (uint32_t)LSM6DSOX_OK;

    status |= (uint32_t)imuManagerSox.end();
    Wire.end();

    if (status != LSM6DSOX_OK)
    {
        return IMU_INIT_ERROR;
    }

    return NO_ERROR;
}

/**
 * @brief Maps an ImuManagerOdr_t selector to its output data rate in Hz.
 * @param odr The output data rate selector.
 * @return The output data rate in Hz, or 0.0f if the selector is unknown.
 */
static float IMUMGR_OdrToNumber(ImuManagerOdr_t odr)
{
    if (odr == IMUMGR_ODR_12_5Hz)
    {
        return 12.5f;
    }
    if (odr == IMUMGR_ODR_26Hz)
    {
        return 26.0f;
    }
    if (odr == IMUMGR_ODR_52Hz)
    {
        return 52.0f;
    }
    if (odr == IMUMGR_ODR_104Hz)
    {
        return 104.0f;
    }
    if (odr == IMUMGR_ODR_208Hz)
    {
        return 208.0f;
    }
    if (odr == IMUMGR_ODR_417Hz)
    {
        return 417.0f;
    }
    if (odr == IMUMGR_ODR_833Hz)
    {
        return 833.0f;
    }
    return 0.0f;
}

/**
 * @brief Maps an ImuManagerAccelRange_t selector to its full-scale range in g.
 * @param accelRange The accelerometer full-scale range selector.
 * @return The full-scale range in g, or 0 if the selector is unknown.
 */
static int32_t IMUMGR_AccelRangeToNumber(ImuManagerAccelRange_t accelRange)
{
    if (accelRange == IMUMGR_ACCEL_FS_2G)
    {
        return 2;
    }
    if (accelRange == IMUMGR_ACCEL_FS_4G)
    {
        return 4;
    }
    if (accelRange == IMUMGR_ACCEL_FS_8G)
    {
        return 8;
    }
    if (accelRange == IMUMGR_ACCEL_FS_16G)
    {
        return 16;
    }
    return 0;
}

/**
 * @brief Maps an ImuManagerGyroRange_t selector to its full-scale range in degrees per second.
 * @param gyroRange The gyroscope full-scale range selector.
 * @return The full-scale range in dps, or 0 if the selector is unknown.
 */
static int32_t IMUMGR_GyroRangeToNumber(ImuManagerGyroRange_t gyroRange)
{
    if (gyroRange == IMUMGR_GYRO_FS_125DPS)
    {
        return 125;
    }
    if (gyroRange == IMUMGR_GYRO_FS_250DPS)
    {
        return 250;
    }
    if (gyroRange == IMUMGR_GYRO_FS_500DPS)
    {
        return 500;
    }
    if (gyroRange == IMUMGR_GYRO_FS_1000DPS)
    {
        return 1000;
    }
    if (gyroRange == IMUMGR_GYRO_FS_2000DPS)
    {
        return 2000;
    }
    return 0;
}

/**
 * @brief Converts an accelerometer reading from milli-g to metres per second squared.
 * @param mg The accelerometer reading in milli-g.
 * @return The acceleration in m/s².
 */
static float IMUMGR_AccelRawToMs2(float mg)
{
    return mg * 0.001f * 9.80665f;
}

/**
 * @brief Converts a gyroscope reading from milli-degrees per second to radians per second.
 * @param mdps The gyroscope reading in milli-degrees per second.
 * @return The angular velocity in rad/s.
 */
static float IMUMGR_GyroRawToRads(float mdps)
{
    return mdps * 0.001f * ((float)M_PI / 180.0f);
}
