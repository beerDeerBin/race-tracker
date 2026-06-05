#include "eeprom_mod.h"
#include "eeprom_mod_internal.h"
#include <EEPROM.h>

// Work variables for the EEPROM module
static EepromWorkVar_t  eepromWorkVar;  // Internal work variables for the EEPROM module
static EepromWorkVar_t* pEepromWorkVar; // Pointer to the internal work variables for the EEPROM module

/**
 * @brief Initializes the EEPROM module by reading the existing data from the EEPROM memory. If the data is invalid
 * (i.e., the magic number or version does not match), it initializes the EEPROM with default values and writes it back
 * to the EEPROM memory. Returns an error code indicating the success or failure of the initialization process.
 *
 * @return ErrorCode_t indicating the success or failure of the initialization process.
 */
ErrorCode_t EEPROM_Init(void)
{
    ErrorCode_t errorCode = NO_ERROR;
    uint32_t    i;

    pEepromWorkVar = &eepromWorkVar;
    memset((void*)pEepromWorkVar, 0x00, sizeof(EepromWorkVar_t));

    if (EEPROM.begin((uint32_t)EEPROM_MODULE_SIZE))
    {
        errorCode |= EEPROM_Read(&pEepromWorkVar->eepromDataBuffer);

        if ((pEepromWorkVar->eepromDataBuffer.magicNumber != (uint32_t)EEPROM_MODULE_MAGIC_NUMBER) ||
            (pEepromWorkVar->eepromDataBuffer.version != (uint32_t)EEPROM_MODULE_VERSION))
        {
            memset((void*)&pEepromWorkVar->eepromDataBuffer, 0x00, sizeof(EepromData_t));
            pEepromWorkVar->eepromDataBuffer.magicNumber = (uint32_t)EEPROM_MODULE_MAGIC_NUMBER;
            pEepromWorkVar->eepromDataBuffer.version     = (uint32_t)EEPROM_MODULE_VERSION;
            for (i = 0; i < 8; i++)
            {
                pEepromWorkVar->eepromDataBuffer.guid.data[i] = 0xFFFF;
            }

            errorCode |= EEPROM_Write(&pEepromWorkVar->eepromDataBuffer);
            errorCode |= EEPROM_INIT_ERROR;
        }
    }
    else
    {
        errorCode = EEPROM_INIT_ERROR;
    }

    return errorCode;
}

/**
 * @brief Reads the EEPROM data structure from the EEPROM memory. Returns an error code indicating the success or
 * failure of the operation.
 *
 * @param pData Pointer to the EEPROM data structure to be read from the EEPROM memory. Must not be nullptr.
 * @return ErrorCode_t indicating the success or failure of the read operation.
 */
ErrorCode_t EEPROM_Read(EepromData_t* pData)
{
    ErrorCode_t errorCode = NO_ERROR;

    if (pData != nullptr)
    {
        EEPROM.get((uint32_t)EEPROM_MODULE_MEM_OFFSET, *pData);
    }
    else
    {
        errorCode = EEPROM_PARAMETER_ERROR;
    }

    return errorCode;
}

/**
 * @brief Writes the provided EEPROM data structure to the EEPROM memory. Returns an error code indicating the success
 * or failure of the operation.
 *
 * @param pData Pointer to the EEPROM data structure to be written to the EEPROM memory. Must not be nullptr.
 * @return ErrorCode_t indicating the success or failure of the write operation.
 */
ErrorCode_t EEPROM_Write(const EepromData_t* pData)
{
    ErrorCode_t errorCode = NO_ERROR;

    if (pData != nullptr)
    {
        EEPROM.put((uint32_t)EEPROM_MODULE_MEM_OFFSET, *pData);
        if (!EEPROM.commit())
        {
            errorCode = EEPROM_WRITE_ERROR;
        }
    }
    else
    {
        errorCode = EEPROM_PARAMETER_ERROR;
    }

    return errorCode;
}
