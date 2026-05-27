#pragma once

#include "../config.h"
#include "../log/log_mod.h"

// EEPROM module name
#define MODULE_EEPROM "EEPROM"

// EEPROM module error codes
#define EEPROM_MODULE_NO_ERROR   0x00
#define EEPROM_MODULE_INIT_ERROR 0x01

// EEPROM module configuration
#define EEPROM_MODULE_SIZE         sizeof(EepromData_t)
#define EEPROM_MODULE_MEM_OFFSET   0x00
#define EEPROM_MODULE_MAGIC_NUMBER 0xAC12DE34
#define EEPROM_MODULE_VERSION      3

// EEPROM module data structure (stored in EEPROM)
typedef struct {
    uint32_t      magicNumber;
    uint32_t      version;
    Guid_t        guid;
    SystemState_t sysState;
    uint32_t      uptimeMs;      // accumulated uptime across deep-sleep cycles
    uint8_t       deepSleepFlag; // set to 1 before deep sleep, checked on boot
} EepromData_t;

// Public function prototypes
void EEPROM_Init(void);
void EEPROM_Read(EepromData_t* pData);
void EEPROM_Write(const EepromData_t* pData);
