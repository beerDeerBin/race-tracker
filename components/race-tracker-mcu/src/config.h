#pragma once

#include <Arduino.h>
#include <freertos/FreeRTOS.h>
#include <freertos/semphr.h>
#include <freertos/task.h>

// ---------------------------------------------------------------------------
//  Gloabl error codes
// ---------------------------------------------------------------------------
// Accumulator / return type: a bitmask that OR-combines named codes up the call stack.
typedef uint64_t ErrorCode_t;

// Named error code values (assigned into / compared against ErrorCode_t bitmasks).
typedef enum
{
    NO_ERROR = 0x00ULL,

    // EEPROM module error codes
    EEPROM_PARAMETER_ERROR = (0x01ULL << 0),
    EEPROM_INIT_ERROR      = (0x01ULL << 1),
    EEPROM_WRITE_ERROR     = (0x01ULL << 2),

    // WiFi module error codes
    WIFI_INIT_ERROR     = (0x01ULL << 8),
    WIFI_CONNECT_ERROR  = (0x01ULL << 9),
    WIFI_SHUTDOWN_ERROR = (0x01ULL << 10),
    WIFI_WAKEUP_ERROR   = (0x01ULL << 11),
    WIFI_SLEEP_ERROR    = (0x01ULL << 12),

    // Data manager error codes
    DAMGR_INIT_ERROR     = (0x01ULL << 16),
    DAMGR_ALLOC_ERROR    = (0x01ULL << 17),
    DAMGR_OVERFLOW_ERROR = (0x01ULL << 18),

    // MQTT module error codes
    MQTT_CONNECT_ERROR   = (0x01ULL << 24),
    MQTT_PUBLISH_ERROR   = (0x01ULL << 25),
    MQTT_SUBSCRIBE_ERROR = (0x01ULL << 26),

    // IMU module error codes
    IMU_INIT_ERROR   = (0x01ULL << 32),
    IMU_CONFIG_ERROR = (0x01ULL << 33),
    IMU_READ_ERROR   = (0x01ULL << 34),
    IMU_FIFO_ERROR   = (0x01ULL << 35),

    // Power management error codes
    PWR_INIT_ERROR             = (0x01ULL << 40),
    PWR_ADC_ERROR              = (0x01ULL << 41),
    PWR_BATTERY_CRITICAL_ERROR = (0x01ULL << 42),
} ErrorCodeValue_t;

// ---------------------------------------------------------------------------
//  Shared project data types
// ---------------------------------------------------------------------------
typedef struct
{
    float ax, ay, az; // Accelerometer data in m/s²
    float gx, gy, gz; // Gyroscope data in rad/s
} SampleRecord_t;

typedef struct
{
    uint16_t data[8];
} Guid_t;

typedef enum
{
    SYS_STATE_IDLE      = 0,
    SYS_STATE_CONNECTED = 1,
    SYS_STATE_ACQUIRING = 2,
} SystemState_t;
