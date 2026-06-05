#include "pwr_mod.h"
#include "pwr_mod_internal.h"

#include <Arduino.h>

// Work variables for the power module
static PwrWorkVar_t  pwrWorkVar;
static PwrWorkVar_t* pPwrWorkVar;

/**
 * @brief Reads the battery voltage on the VBAT sense pin, averaging several samples and compensating for the on-board
 * 2:1 resistor divider.
 * @return The measured battery voltage in millivolts.
 */
static float _PWR_ReadVbatMv(void)
{
    uint32_t sum = 0;
    for (int i = 0; i < 16; i++)
    {
        sum += analogReadMilliVolts(PWR_PIN_VBAT_SENSE);
        delayMicroseconds(200);
    }
    return (float)(sum >> 4) * 2.0f;
}

/**
 * @brief Updates the cached power state (normal / low / critical) from the most recently measured battery voltage.
 */
static void _PWR_UpdateState(void)
{
    if (pPwrWorkVar->lastVbatMv < (float)PWR_VBAT_CRITICAL_MV)
    {
        pPwrWorkVar->state = PWR_STATE_CRITICAL_BATTERY;
    }
    else if (pPwrWorkVar->lastVbatMv < (float)PWR_VBAT_LOW_MV)
    {
        pPwrWorkVar->state = PWR_STATE_LOW_BATTERY;
    }
    else
    {
        pPwrWorkVar->state = PWR_STATE_NORMAL;
    }
}

/**
 * @brief Initializes the power module, capturing the current CPU frequency and taking an initial battery measurement.
 * Must be called once at startup before any other power function.
 */
void PWR_Init(void)
{
    pPwrWorkVar = &pwrWorkVar;
    memset((void*)pPwrWorkVar, 0x00, sizeof(PwrWorkVar_t));
    pPwrWorkVar->currentCpuMhz = getCpuFrequencyMhz();

    pPwrWorkVar->lastVbatMv = _PWR_ReadVbatMv();
    _PWR_UpdateState();
}

/**
 * @brief Re-measures the battery voltage and refreshes the cached power state. Measurement only — the device no longer
 * sleeps; a critical battery is surfaced via PWR_GetState() and reported in the health message.
 */
void PWR_Poll(void)
{
    pPwrWorkVar->lastVbatMv = _PWR_ReadVbatMv();
    _PWR_UpdateState();
}

/**
 * @brief Sets the CPU frequency, skipping the change if it already matches the requested value.
 * @param mhz The desired CPU frequency in MHz.
 */
void PWR_SetCpuFreq(uint32_t mhz)
{
    if (mhz == pPwrWorkVar->currentCpuMhz)
    {
        return;
    }
    setCpuFrequencyMhz(mhz);
    pPwrWorkVar->currentCpuMhz = mhz;
}

/**
 * @brief Returns the most recently measured battery voltage.
 * @return The battery voltage in millivolts.
 */
float PWR_GetBatteryMv(void)
{
    return pPwrWorkVar->lastVbatMv;
}

/**
 * @brief Estimates the battery charge from the most recent voltage measurement, clamped to the 0..100 range.
 * @return The estimated battery charge as a percentage (0-100).
 */
uint8_t PWR_GetBatteryPct(void)
{
    float pct = (pPwrWorkVar->lastVbatMv - 3000.0f) / (4200.0f - 3000.0f) * 100.0f;
    if (pct < 0.0f)
    {
        pct = 0.0f;
    }
    if (pct > 100.0f)
    {
        pct = 100.0f;
    }
    return (uint8_t)pct;
}

/**
 * @brief Returns the cached power state as determined by the last battery measurement.
 * @return The current PwrState_t.
 */
PwrState_t PWR_GetState(void)
{
    return pPwrWorkVar->state;
}
