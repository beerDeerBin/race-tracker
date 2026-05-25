#include "eeprom_mod.h"
#include "eeprom_mod_internal.h"
#include <EEPROM.h>

// Work variables for the EEPROM module
static EepromWorkVar_t  eepromWorkVar;  // Internal work variables for the EEPROM module
static EepromWorkVar_t* pEepromWorkVar; // Pointer to the internal work variables for the EEPROM module

static EepromData_t eepromDataBuffer;   // EEPROM data structure buffer

void EEPROM_Init(void) {
    pEepromWorkVar = &eepromWorkVar;

    memset((void*)pEepromWorkVar, 0x00, sizeof(EepromWorkVar_t));
    pEepromWorkVar->pEepromDataBuffer = &eepromDataBuffer;

    EEPROM.begin((uint32_t)EEPROM_MODULE_SIZE);
    EEPROM_Read(pEepromWorkVar->pEepromDataBuffer);

    if ((pEepromWorkVar->pEepromDataBuffer->magicNumber != (uint32_t)EEPROM_MODULE_MAGIC_NUMBER) ||
        (pEepromWorkVar->pEepromDataBuffer->version != (uint32_t)EEPROM_MODULE_VERSION)) {

        LOG_WARNING(MODULE_EEPROM, EEPROM_MODULE_INIT_ERROR, "invalid data in EEPROM");

        pEepromWorkVar->pEepromDataBuffer->magicNumber = (uint32_t)EEPROM_MODULE_MAGIC_NUMBER;
        pEepromWorkVar->pEepromDataBuffer->version     = (uint32_t)EEPROM_MODULE_VERSION;
        EEPROM_Write(pEepromWorkVar->pEepromDataBuffer);
    }
}

void EEPROM_Read(EepromData_t* pData) {
    EEPROM.get((uint32_t)EEPROM_MODULE_MEM_OFFSET, *pData);
}

void EEPROM_Write(const EepromData_t* pData) {
    EEPROM.put((uint32_t)EEPROM_MODULE_MEM_OFFSET, *pData);
    EEPROM.commit();
}
