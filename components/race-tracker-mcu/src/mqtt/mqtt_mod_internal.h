#pragma once

// Internal work variables for the MQTT module
typedef struct {
    char              clientId[48]; // "rt-<uuid>" — UUID is 36 chars, prefix 3, total 39
    char              dataTopic[48];
    char              statusTopic[48];
    char              cmdTopic[48];
    uint8_t           lastCmd;
    MqttCmdStartRun_t cmdStartRun;
    SampleRecord_t    batchBuf[MQTT_MODULE_BATCH_MAX]; // pop target for PublishBatch
} MqttWorkVar_t;

static void MQTT_OnMessage(char* topic, uint8_t* payload, unsigned int length);
