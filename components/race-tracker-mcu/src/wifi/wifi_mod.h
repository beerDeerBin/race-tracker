#pragma once

#include "../log/log_mod.h"
#include "../secrets.h"

// Wifi module name
#define MODULE_WIFI "WIFI"

// Wifi module error codes
#define WIFI_MODULE_NO_ERROR       0x00
#define WIFI_MODULE_CONNECT_ERROR  0x01
#define WIFI_MODULE_INIT_ERROR     (0x01 << 1)
#define WIFI_MODULE_SHUTDOWN_ERROR (0x01 << 2)
#define WIFI_MODULE_WAKEUP_ERROR   (0x01 << 3)
#define WIFI_MODULE_SLEEP_ERROR    (0x01 << 4)

// Wifi module configuration
#define WIFI_MODULE_CONNECT_TIMEOUT_MS 10000

// Public function prototypes
void WIFI_Init(void);
bool WIFI_Connect(void);
bool WIFI_IsConnected(void);
void WIFI_Shutdown(void);
void WIFI_Wakeup(void);
void WIFI_EnableModemSleep(void);
void WIFI_DisableModemSleep(void);
