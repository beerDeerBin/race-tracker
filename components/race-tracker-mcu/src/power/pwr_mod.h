#pragma once

#include "../config.h"

// Board: Adafruit ESP32 Feather V2
// A13 routes to an internal 200K+200K divider on the BAT pin (not exposed externally)
#define PWR_PIN_VBAT_SENSE       A13
#define PWR_VBAT_CRITICAL_MV     3100 // ~8%  — 100 mV above hard cutoff
#define PWR_VBAT_LOW_MV          3400 // ~33% — early warning
#define PWR_BATTERY_CAPACITY_MAH 350  // LP-552035

// Single fixed operating frequency. Switching CPU frequency at runtime with WiFi connected
// desyncs the radio/socket, so the device sets this once at boot (before WiFi) and never changes it.
#define PWR_CPU_FREQ_IDLE_MHZ 80

typedef enum
{
    PWR_STATE_NORMAL           = 0,
    PWR_STATE_LOW_BATTERY      = 1,
    PWR_STATE_CRITICAL_BATTERY = 2,
} PwrState_t;

// Public function prototypes
void       PWR_Init(void);
void       PWR_Poll(void);
void       PWR_SetCpuFreq(uint32_t mhz);
float      PWR_GetBatteryMv(void);
uint8_t    PWR_GetBatteryPct(void);
PwrState_t PWR_GetState(void);
