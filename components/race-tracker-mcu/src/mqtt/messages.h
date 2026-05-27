#pragma once

// Topic fragments — full topics are built at init: "rt/<deviceId>/data" etc.
#define MQTT_TOPIC_ROOT   "rt"
#define MQTT_TOPIC_DATA   "data"
#define MQTT_TOPIC_STATUS "status"
#define MQTT_TOPIC_CMD    "cmd"

// Command type constants (internal)
#define MQTT_CMD_NONE      0x00
#define MQTT_CMD_CONNECT   0x01
#define MQTT_CMD_START_RUN 0x02

// Inbound: parsed start_run parameters (follows MQTT_CMD_START_RUN byte)
typedef struct __attribute__((packed)) {
    uint32_t runId;
    uint32_t numSamples;
    uint8_t  odr;        // ImuManagerOdr_t value
    uint8_t  accelRange; // ImuManagerAccelRange_t value
    uint8_t  gyroRange;  // ImuManagerGyroRange_t value
} MqttCmdStartRun_t;

// Outbound: keepalive payload
typedef struct __attribute__((packed)) {
    uint32_t uptimeMs;
    uint8_t  status; // 0 = idle, 1 = running
} MqttStatus_t;

// Outbound: batch header (precedes SampleRecord_t array)
typedef struct __attribute__((packed)) {
    uint32_t runId;
    uint32_t startOffset;
    uint32_t count;
} MqttBatchHeader_t;
