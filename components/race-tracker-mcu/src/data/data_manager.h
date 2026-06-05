#pragma once

#include "../config.h"

// Data manager configuration
#define DAMGR_PSRAM_SIZE_BYTES (15UL * 1024UL * 128UL)
#define DAMGR_CAPACITY         (DAMGR_PSRAM_SIZE_BYTES / sizeof(SampleRecord_t))

// Public function prototypes
ErrorCode_t DAMGR_Init(void);
void        DAMGR_Reserve(SampleRecord_t** ppOut, uint32_t* pId);
ErrorCode_t DAMGR_Push(uint32_t id);
ErrorCode_t DAMGR_Pop(SampleRecord_t* pDst, uint32_t numOfRecords);
uint32_t    DAMGR_Count(void);
void        DAMGR_Clear(void);
