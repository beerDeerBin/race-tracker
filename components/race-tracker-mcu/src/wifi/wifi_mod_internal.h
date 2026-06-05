#pragma once

// Internal work variables for the Wifi module
typedef struct
{
    bool    isConnected;
    uint8_t reserved[3]; // Padding for 4-byte alignment
} WifiWorkVar_t;
