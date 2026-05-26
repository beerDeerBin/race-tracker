#pragma once

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
#define EEPROM_MODULE_VERSION      1

// EEPROM module data structure (stored in EEPROM)
typedef struct {
    uint32_t magicNumber; // Magic number to validate EEPROM data
    uint32_t version;     // Version of the EEPROM data structure
    uint32_t id;          // Unique identifier for the device or configuration
    uint32_t status;      // TODO
} EepromData_t;

// Public function prototypes
void EEPROM_Init(void);
void EEPROM_Read(EepromData_t* pData);
void EEPROM_Write(const EepromData_t* pData);
