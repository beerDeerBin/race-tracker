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

    if (!EEPROM.begin((uint32_t)EEPROM_MODULE_SIZE)) {
        LOG_ERROR(MODULE_EEPROM, EEPROM_MODULE_INIT_ERROR, "failed to initialize EEPROM");
        return;
    }

    EEPROM_Read(pEepromWorkVar->pEepromDataBuffer);

    if ((pEepromWorkVar->pEepromDataBuffer->magicNumber != (uint32_t)EEPROM_MODULE_MAGIC_NUMBER) ||
        (pEepromWorkVar->pEepromDataBuffer->version != (uint32_t)EEPROM_MODULE_VERSION)) {

        LOG_WARNING(MODULE_EEPROM, EEPROM_MODULE_INIT_ERROR, "invalid data in EEPROM, resetting to defaults");

        memset((void*)pEepromWorkVar->pEepromDataBuffer, 0x00, sizeof(EepromData_t));
        pEepromWorkVar->pEepromDataBuffer->magicNumber  = (uint32_t)EEPROM_MODULE_MAGIC_NUMBER;
        pEepromWorkVar->pEepromDataBuffer->version      = (uint32_t)EEPROM_MODULE_VERSION;
        pEepromWorkVar->pEepromDataBuffer->guid.data[0] = 0xDA46;
        pEepromWorkVar->pEepromDataBuffer->guid.data[1] = 0xCA23;
        pEepromWorkVar->pEepromDataBuffer->guid.data[2] = 0x7203;
        pEepromWorkVar->pEepromDataBuffer->guid.data[3] = 0x4015;
        pEepromWorkVar->pEepromDataBuffer->guid.data[4] = 0x94B1;
        pEepromWorkVar->pEepromDataBuffer->guid.data[5] = 0xB597;
        pEepromWorkVar->pEepromDataBuffer->guid.data[6] = 0x892C;
        pEepromWorkVar->pEepromDataBuffer->guid.data[7] = 0xABEB;
        pEepromWorkVar->pEepromDataBuffer->sysState     = SYS_STATE_IDLE;
        EEPROM_Write(pEepromWorkVar->pEepromDataBuffer);
        return;
    }

    LOG_INFO(MODULE_EEPROM, EEPROM_MODULE_NO_ERROR, "initialized successfully");
}

void EEPROM_Read(EepromData_t* pData) {
    EEPROM.get((uint32_t)EEPROM_MODULE_MEM_OFFSET, *pData);
}

void EEPROM_Write(const EepromData_t* pData) {
    EEPROM.put((uint32_t)EEPROM_MODULE_MEM_OFFSET, *pData);
    EEPROM.commit();
}
