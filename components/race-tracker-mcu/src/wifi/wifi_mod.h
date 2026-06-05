#pragma once

#include "../config.h"
#include "../secrets.h"

// Wifi module configuration
#define WIFI_MODULE_CONNECT_TIMEOUT_MS 5000

// Public function prototypes
ErrorCode_t WIFI_Init(void);
bool        WIFI_Connect(void);
bool        WIFI_IsConnected(void);
ErrorCode_t WIFI_Shutdown(void);
ErrorCode_t WIFI_Wakeup(void);
ErrorCode_t WIFI_EnableModemSleep(void);
ErrorCode_t WIFI_DisableModemSleep(void);
