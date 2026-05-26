#pragma once

#include "../log/log_mod.h"

// Data manager name
#define MODULE_DAMGR "DATA_MGR"

// Data manager error codes
#define DAMGR_NO_ERROR       0x00
#define DAMGR_INIT_ERROR     0x01
#define DAMGR_ALLOC_ERROR    (0x01 << 1)
#define DAMGR_OVERFLOW_ERROR (0x01 << 2)

// Data manager configuration
#define DAMGR_PSRAM_SIZE_BYTES (15UL * 1024UL * 128UL)
#define DAMGR_CAPACITY         (DAMGR_PSRAM_SIZE_BYTES / sizeof(SampleRecord_t))

// Public function prototypes
void     DAMGR_Init(void);
void     DAMGR_Reserve(SampleRecord_t** ppOut, uint32_t* pId);
void     DAMGR_Push(uint32_t id);
void     DAMGR_Pop(SampleRecord_t* pDst, uint32_t numOfRecords);
uint32_t DAMGR_Count(void);
void     DAMGR_Clear(void);
