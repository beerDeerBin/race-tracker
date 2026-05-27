#include "pwr_mod.h"
#include "pwr_mod_internal.h"

#include <Arduino.h>
#include <esp_sleep.h>

// Work variables for the power module
static PwrWorkVar_t  pwrWorkVar;
static PwrWorkVar_t* pPwrWorkVar;

static float _PWR_ReadVbatMv(void) {
    uint32_t sum = 0;
    for (int i = 0; i < 16; i++) {
        sum += analogReadMilliVolts(PWR_PIN_VBAT_SENSE);
        delayMicroseconds(200);
    }
    return (float)(sum >> 4) * 2.0f;
}

static void _PWR_UpdateState(void) {
    if (pPwrWorkVar->lastVbatMv < (float)PWR_VBAT_CRITICAL_MV) {
        pPwrWorkVar->state = PWR_STATE_CRITICAL_BATTERY;
    } else if (pPwrWorkVar->lastVbatMv < (float)PWR_VBAT_LOW_MV) {
        pPwrWorkVar->state = PWR_STATE_LOW_BATTERY;
    } else {
        pPwrWorkVar->state = PWR_STATE_NORMAL;
    }
}

void PWR_Init(void) {
    esp_sleep_wakeup_cause_t cause;

    pPwrWorkVar = &pwrWorkVar;
    memset((void*)pPwrWorkVar, 0x00, sizeof(PwrWorkVar_t));
    pPwrWorkVar->currentCpuMhz = getCpuFrequencyMhz();

    pPwrWorkVar->lastVbatMv = _PWR_ReadVbatMv();
    _PWR_UpdateState();

    cause = esp_sleep_get_wakeup_cause();
    if (cause == ESP_SLEEP_WAKEUP_TIMER) {
        LOG_INFO(MODULE_PWR, PWR_MODULE_NO_ERROR, "woke (timer)");
    } else if (cause == ESP_SLEEP_WAKEUP_EXT0 || cause == ESP_SLEEP_WAKEUP_EXT1) {
        LOG_INFO(MODULE_PWR, PWR_MODULE_NO_ERROR, "woke (GPIO)");
    } else {
        LOG_INFO(MODULE_PWR, PWR_MODULE_NO_ERROR, "power-on reset");
    }

    LOG_INFO(MODULE_PWR, PWR_MODULE_NO_ERROR, "battery: %.0f mV (%u%%)", pPwrWorkVar->lastVbatMv,
             PWR_GetBatteryPct());
    LOG_INFO(MODULE_PWR, PWR_MODULE_NO_ERROR, "initialized, CPU @ %u MHz", pPwrWorkVar->currentCpuMhz);
}

void PWR_Poll(void) {
    pPwrWorkVar->lastVbatMv = _PWR_ReadVbatMv();
    _PWR_UpdateState();

    LOG_INFO(MODULE_PWR, PWR_MODULE_NO_ERROR, "battery: %.0f mV (%u%%), state: %u", pPwrWorkVar->lastVbatMv,
             PWR_GetBatteryPct(), (uint8_t)pPwrWorkVar->state);

    if (pPwrWorkVar->state == PWR_STATE_CRITICAL_BATTERY) {
        LOG_ERROR(MODULE_PWR, PWR_MODULE_NO_ERROR, "critical battery — entering deep sleep");
        PWR_DeepSleep(0);
    }
}

void PWR_DeepSleep(uint32_t sleepMs) {
    if (sleepMs > 0) {
        LOG_INFO(MODULE_PWR, PWR_MODULE_NO_ERROR, "deep sleep for %lu ms", sleepMs);
    } else {
        LOG_INFO(MODULE_PWR, PWR_MODULE_NO_ERROR, "deep sleep indefinitely");
    }

    Serial.flush();

    if (sleepMs > 0) { esp_sleep_enable_timer_wakeup((uint64_t)sleepMs * 1000ULL); }
    esp_deep_sleep_start();
}

void PWR_LightSleep(uint32_t durationMs) {
    esp_sleep_enable_timer_wakeup((uint64_t)durationMs * 1000ULL);
    esp_light_sleep_start();
}

void PWR_SetCpuFreq(uint32_t mhz) {
    if (mhz == pPwrWorkVar->currentCpuMhz) return;
    setCpuFrequencyMhz(mhz);
    pPwrWorkVar->currentCpuMhz = mhz;
    LOG_INFO(MODULE_PWR, PWR_MODULE_NO_ERROR, "CPU @ %u MHz", mhz);
}

bool PWR_WokeFromDeepSleep(void) {
    return esp_sleep_get_wakeup_cause() == ESP_SLEEP_WAKEUP_TIMER;
}

float PWR_GetBatteryMv(void) {
    return pPwrWorkVar->lastVbatMv;
}

uint8_t PWR_GetBatteryPct(void) {
    float pct = (pPwrWorkVar->lastVbatMv - 3000.0f) / (4200.0f - 3000.0f) * 100.0f;
    if (pct < 0.0f) pct = 0.0f;
    if (pct > 100.0f) pct = 100.0f;
    return (uint8_t)pct;
}

PwrState_t PWR_GetState(void) {
    return pPwrWorkVar->state;
}
