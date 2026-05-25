#pragma once

#include <stdarg.h>

// Internal function prototypes for the Log pModule
static char* LOG_GetLevelString(uint8_t level);
static void  LOG_Print(uint8_t level, const char* pModule, uint32_t errorCode, const char* pMsg, va_list args);
