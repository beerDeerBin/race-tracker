#pragma once

// Internal work variables for the EEPROM module
typedef struct {
    EepromData_t* pEepromDataBuffer; // Pointer to the EEPROM data buffer
} EepromWorkVar_t;