#pragma once

#include <Arduino.h>
#include <freertos/FreeRTOS.h>
#include <freertos/semphr.h>
#include <freertos/task.h>

// ---------------------------------------------------------------------------
//  Shared project data types
// ---------------------------------------------------------------------------
typedef struct {
    float ax, ay, az; // Accelerometer data in m/s²
    float gx, gy, gz; // Gyroscope data in rad/s
} SampleRecord_t;

typedef struct {
    uint16_t data[8];
} Guid_t;
