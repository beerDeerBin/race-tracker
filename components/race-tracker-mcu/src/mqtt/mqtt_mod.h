#pragma once

#include "../config.h"
#include "../data/data_manager.h"
#include "../secrets.h"
#include "messages.h"

// MQTT module configuration
#define MQTT_MODULE_BATCH_MAX   32  // max SampleRecord_t per publishBatch call
#define MQTT_MODULE_BUFFER_SIZE 256 // inbound command buffer (binary commands are small)

// Public function prototypes
ErrorCode_t MQTT_Init(const Guid_t* pGuid);
bool        MQTT_Connect(void);
void        MQTT_Disconnect(void);
bool        MQTT_IsConnected(void);
ErrorCode_t MQTT_PublishKeepalive(const MqttStatus_t* pStatus);
ErrorCode_t MQTT_PublishBatch(const Guid_t* pRunId, uint32_t startOffset);
uint8_t     MQTT_PollCommand(MqttCmdStartRun_t* pOut);
