#pragma once

#include "../config.h"

// Log module error codes
#define LOG_MODULE_NO_ERROR 0

// Log module configuration
#define LOG_MODULE_BAUD_RATE     115200
#define LOG_MODULE_INIT_DELAY_MS 5000

// Log module names
#define MODULE_LOG    "LOG"
#define MODULE_EEPROM "EEPROM"
#define MODULE_DAMGR  "DATA_MGR"
#define MODULE_IMUMGR "IMU_MGR"

// Log module guard for compilation optimization
#ifndef LOG_ENABLED
#define LOG_ENABLED 1
#endif

#if LOG_ENABLED
#define LOG_ERROR(mod, code, msg, ...)   LOG_Error((mod), (code), (msg), ##__VA_ARGS__)
#define LOG_WARNING(mod, code, msg, ...) LOG_Warning((mod), (code), (msg), ##__VA_ARGS__)
#define LOG_INFO(mod, code, msg, ...)    LOG_Info((mod), (code), (msg), ##__VA_ARGS__)
#else
#define LOG_ERROR(mod, code, msg, ...)   ((void)0)
#define LOG_WARNING(mod, code, msg, ...) ((void)0)
#define LOG_INFO(mod, code, msg, ...)    ((void)0)
#endif

// Public function prototypes
void LOG_Init(void);
void LOG_Error(const char* pModule, uint32_t errorCode, const char* pMsg, ...);
void LOG_Warning(const char* pModule, uint32_t errorCode, const char* pMsg, ...);
void LOG_Info(const char* pModule, uint32_t errorCode, const char* pMsg, ...);
