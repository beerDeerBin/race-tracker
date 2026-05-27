#pragma once

// Internal work variables for the power module
typedef struct {
    PwrState_t state;
    float      lastVbatMv;
    uint32_t   lastBatCheckMs;
    uint32_t   currentCpuMhz;
} PwrWorkVar_t;

static float _PWR_ReadVbatMv(void);
static void  _PWR_UpdateState(void);
