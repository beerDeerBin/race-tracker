#pragma once

// Topic fragments — full topics are built at init: "rt/<deviceId>/data" etc.
#define MQTT_TOPIC_ROOT   "rt"
#define MQTT_TOPIC_DATA   "data"
#define MQTT_TOPIC_STATUS "status"
#define MQTT_TOPIC_CMD    "cmd"

// Command type constants (internal)
#define MQTT_CMD_NONE       0x00
#define MQTT_CMD_CONNECT    0x01
#define MQTT_CMD_START_RUN  0x02
#define MQTT_CMD_DISCONNECT 0x03
#define MQTT_CMD_RESET      0x04

// Inbound: parsed start_run parameters (follows MQTT_CMD_START_RUN byte)
typedef struct
{
    Guid_t   runId;
    uint32_t numSamples;
    uint8_t  odr;        // ImuManagerOdr_t value
    uint8_t  accelRange; // ImuManagerAccelRange_t value
    uint8_t  gyroRange;  // ImuManagerGyroRange_t value
    uint8_t  reserved;   // buffer for alignment
} MqttCmdStartRun_t;

// Outbound: keepalive / health payload
typedef struct
{
    uint32_t uptimeMs;     // milliseconds since last fresh boot / reset
    uint16_t batteryMv;    // battery voltage in millivolts, or 65535 if unknown
    uint8_t  batteryPct;   // 0-100, or 255 if unknown
    uint8_t  status;       // SystemState_t value
    uint32_t sampledCount; // samples collected so far (0 when not acquiring)
    uint32_t totalSamples; // total samples requested for current run (0 when not acquiring)
    uint64_t errorCode;    // ErrorCodeValue_t bitmask — accumulated faults | live conditions
} MqttStatus_t;

// Outbound: batch header (precedes SampleRecord_t array)
typedef struct
{
    Guid_t   runId;
    uint32_t startOffset;
    uint32_t count;
} MqttBatchHeader_t;
