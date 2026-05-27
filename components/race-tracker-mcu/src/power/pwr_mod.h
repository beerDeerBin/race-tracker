#pragma once

#include "../log/log_mod.h"

// Power module name
#define MODULE_PWR "PWR"

// Power module error codes
#define PWR_MODULE_NO_ERROR   0x00
#define PWR_MODULE_INIT_ERROR 0x01
#define PWR_MODULE_ADC_ERROR  (0x01 << 1)

// Board: Adafruit ESP32 Feather V2
// A13 routes to an internal 200K+200K divider on the BAT pin (not exposed externally)
#define PWR_PIN_VBAT_SENSE         A13
#define PWR_VBAT_CHECK_INTERVAL_MS 30000 // 30 s
#define PWR_VBAT_CRITICAL_MV       3100  // ~8%  — 100 mV above hard cutoff
#define PWR_VBAT_LOW_MV            3400  // ~33% — early warning
#define PWR_BATTERY_CAPACITY_MAH   350   // LP-552035

#define PWR_CPU_FREQ_ACTIVE_MHZ 240
#define PWR_CPU_FREQ_IDLE_MHZ   80

typedef enum {
    PWR_STATE_NORMAL           = 0,
    PWR_STATE_LOW_BATTERY      = 1,
    PWR_STATE_CRITICAL_BATTERY = 2,
} PwrState_t;

// Public function prototypes
void       PWR_Init(void);
void       PWR_Poll(void);                  // call each loop iteration; auto deep-sleeps if critical
void       PWR_DeepSleep(uint32_t sleepMs); // caller must shut down WiFi/MQTT first
void       PWR_LightSleep(uint32_t durationMs);
void       PWR_SetCpuFreq(uint32_t mhz);
float      PWR_GetBatteryMv(void);
uint8_t    PWR_GetBatteryPct(void);
PwrState_t PWR_GetState(void);
