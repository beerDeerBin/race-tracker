#pragma once

#include "../data/data_manager.h"
#include "../log/log_mod.h"
#include "../secrets.h"
#include "messages.h"

// MQTT module name
#define MODULE_MQTT "MQTT"

// MQTT module error codes
#define MQTT_MODULE_NO_ERROR        0x00
#define MQTT_MODULE_CONNECT_ERROR   0x01
#define MQTT_MODULE_PUBLISH_ERROR   (0x01 << 1)
#define MQTT_MODULE_SUBSCRIBE_ERROR (0x01 << 2)

// MQTT module configuration
#define MQTT_MODULE_BATCH_MAX   32   // max SampleRecord_t per publishBatch call
#define MQTT_MODULE_BUFFER_SIZE 256  // inbound command buffer (binary commands are small)

// Public function prototypes
void    MQTT_Init(const Guid_t* pGuid);
bool    MQTT_Connect(void);
void    MQTT_Disconnect(void);
bool    MQTT_IsConnected(void);
void    MQTT_PublishKeepalive(const MqttStatus_t* pStatus);
void    MQTT_PublishBatch(const Guid_t* pRunId, uint32_t startOffset);
uint8_t MQTT_PollCommand(MqttCmdStartRun_t* pOut);
