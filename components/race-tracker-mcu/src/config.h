#pragma once

#include <Arduino.h>
#include <freertos/FreeRTOS.h>
#include <freertos/semphr.h>
#include <freertos/task.h>

// ---------------------------------------------------------------------------
//  Shared project data types
// ---------------------------------------------------------------------------
typedef struct {
    uint32_t id;         // Id of the measurement
    uint32_t offset;     // offset of this record from the start of the measurement session
    float    ax, ay, az; // Accelerometer data in m/s²
    float    gx, gy, gz; // Gyroscope data in rad/s
} SampleRecord_t;
