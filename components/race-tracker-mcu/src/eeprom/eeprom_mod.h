#pragma once

#include "../config.h"

// EEPROM module constants
#define EEPROM_MODULE_SIZE         sizeof(EepromData_t)
#define EEPROM_MODULE_MEM_OFFSET   0x00
#define EEPROM_MODULE_MAGIC_NUMBER 0xAC12DE34
#define EEPROM_MODULE_VERSION      4

// EEPROM module data structure (stored in EEPROM)
typedef struct
{
    uint32_t magicNumber;
    uint32_t version;
    Guid_t   guid;
} EepromData_t;

// Public function prototypes
ErrorCode_t EEPROM_Init(void);
ErrorCode_t EEPROM_Read(EepromData_t* pData);
ErrorCode_t EEPROM_Write(const EepromData_t* pData);
