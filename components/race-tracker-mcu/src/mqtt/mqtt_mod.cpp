#include "mqtt_mod.h"
#include "mqtt_mod_internal.h"

#include <PubSubClient.h>
#include <WiFi.h>

// C++ objects must live outside the work-var (memset would destroy them)
static WiFiClient   mqttWifiClient;
static PubSubClient mqttClient(mqttWifiClient);

// Work variables for the MQTT module
static MqttWorkVar_t  mqttWorkVar;
static MqttWorkVar_t* pMqttWorkVar;

/**
 * @brief Initializes the MQTT module by deriving the client id and topic strings from the device GUID and configuring
 * the underlying MQTT client (broker, keepalive, buffer size and inbound message callback). Must be called once before
 * any other MQTT function.
 * @param pGuid Pointer to the device GUID used to build the unique client id and topics.
 * @return ErrorCode_t indicating the success or failure of the initialization process.
 */
ErrorCode_t MQTT_Init(const Guid_t* pGuid)
{
    pMqttWorkVar = &mqttWorkVar;
    memset((void*)pMqttWorkVar, 0x00, sizeof(MqttWorkVar_t));

    // Format GUID as standard UUID string: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
    char uuid[37];
    snprintf(uuid, sizeof(uuid), "%04X%04X-%04X-%04X-%04X-%04X%04X%04X", pGuid->data[0], pGuid->data[1], pGuid->data[2],
             pGuid->data[3], pGuid->data[4], pGuid->data[5], pGuid->data[6], pGuid->data[7]);

    snprintf(pMqttWorkVar->clientId, sizeof(pMqttWorkVar->clientId), "%s-%s", MQTT_TOPIC_ROOT, uuid);
    snprintf(pMqttWorkVar->dataTopic, sizeof(pMqttWorkVar->dataTopic), "%s/%s/%s", MQTT_TOPIC_ROOT, uuid,
             MQTT_TOPIC_DATA);
    snprintf(pMqttWorkVar->statusTopic, sizeof(pMqttWorkVar->statusTopic), "%s/%s/%s", MQTT_TOPIC_ROOT, uuid,
             MQTT_TOPIC_STATUS);
    snprintf(pMqttWorkVar->cmdTopic, sizeof(pMqttWorkVar->cmdTopic), "%s/%s/%s", MQTT_TOPIC_ROOT, uuid, MQTT_TOPIC_CMD);

    mqttClient.setServer(MQTT_BROKER_HOST, MQTT_BROKER_PORT);
    mqttClient.setKeepAlive(MQTT_KEEPALIVE);
    if (!mqttClient.setBufferSize(MQTT_MODULE_BUFFER_SIZE))
    {
        return MQTT_CONNECT_ERROR;
    }
    mqttClient.setCallback(MQTT_OnMessage);

    return NO_ERROR;
}

/**
 * @brief Connects to the MQTT broker using the configured credentials and, on success, subscribes to the inbound
 * command topic.
 * @return true if the connection (and subscription) succeeded, false otherwise.
 */
bool MQTT_Connect(void)
{
    bool ok = mqttClient.connect(pMqttWorkVar->clientId, MQTT_BROKER_USER, MQTT_BROKER_PASSWORD, nullptr, 0, false,
                                 nullptr, false);
    if (!ok)
    {
        return false;
    }

    mqttClient.subscribe(pMqttWorkVar->cmdTopic, 1);
    return true;
}

/**
 * @brief Disconnects the MQTT client from the broker.
 */
void MQTT_Disconnect(void)
{
    mqttClient.disconnect();
}

/**
 * @brief Checks whether the MQTT client is currently connected to the broker.
 * @return true if connected, false otherwise.
 */
bool MQTT_IsConnected(void)
{
    return mqttClient.connected();
}

/**
 * @brief Publishes a status/keepalive message to the device status topic.
 * @param pStatus Pointer to the status payload to publish.
 * @return ErrorCode_t indicating the success or failure of the publish.
 */
ErrorCode_t MQTT_PublishKeepalive(const MqttStatus_t* pStatus)
{
    if (!mqttClient.publish(pMqttWorkVar->statusTopic, (uint8_t*)pStatus, sizeof(MqttStatus_t), false))
    {
        return MQTT_PUBLISH_ERROR;
    }
    return NO_ERROR;
}

/**
 * @brief Pops up to MQTT_MODULE_BATCH_MAX buffered records from the data manager and publishes them to the data topic
 * as a single message prefixed with a MqttBatchHeader_t. Does nothing if no records are buffered.
 * @param pRunId Pointer to the run id that the batch belongs to.
 * @param startOffset Index of the first record in this batch within the overall run.
 * @return ErrorCode_t indicating the success or failure of the operation.
 */
ErrorCode_t MQTT_PublishBatch(const Guid_t* pRunId, uint32_t startOffset)
{
    ErrorCode_t errorCode = NO_ERROR;

    uint32_t count = min(DAMGR_Count(), (uint32_t)MQTT_MODULE_BATCH_MAX);
    if (count == 0)
    {
        return NO_ERROR;
    }

    errorCode |= DAMGR_Pop(pMqttWorkVar->batchBuf, count);

    MqttBatchHeader_t header    = {*pRunId, startOffset, count};
    uint32_t          totalSize = sizeof(MqttBatchHeader_t) + count * sizeof(SampleRecord_t);

    if (!mqttClient.beginPublish(pMqttWorkVar->dataTopic, totalSize, false))
    {
        return errorCode | MQTT_PUBLISH_ERROR;
    }
    mqttClient.write((uint8_t*)&header, sizeof(header));
    mqttClient.write((uint8_t*)pMqttWorkVar->batchBuf, count * sizeof(SampleRecord_t));
    mqttClient.endPublish();

    return errorCode;
}

/**
 * @brief Services the MQTT client loop and returns the most recently received command, if any. For MQTT_CMD_START_RUN
 * the associated run parameters are copied into pOut. The pending command is consumed (reset to MQTT_CMD_NONE) by this
 * call.
 * @param pOut Output buffer that receives the run parameters when a start-run command is pending. May be NULL.
 * @return The pending command code, or MQTT_CMD_NONE if no command is pending.
 */
uint8_t MQTT_PollCommand(MqttCmdStartRun_t* pOut)
{
    mqttClient.loop();

    uint8_t cmd = pMqttWorkVar->lastCmd;
    if (cmd == MQTT_CMD_START_RUN && pOut != NULL)
    {
        memcpy(pOut, &pMqttWorkVar->cmdStartRun, sizeof(MqttCmdStartRun_t));
    }

    pMqttWorkVar->lastCmd = MQTT_CMD_NONE;
    return cmd;
}

/**
 * @brief Inbound MQTT message callback. Validates the command byte (and the payload length for start-run) and stores
 * the recognized command for the next MQTT_PollCommand call. Unrecognized messages are ignored.
 * @param topic The topic the message arrived on (unused).
 * @param payload The raw message payload; the first byte is the command code.
 * @param length The payload length in bytes.
 */
static void MQTT_OnMessage(char* topic, uint8_t* payload, unsigned int length)
{
    uint8_t cmd = payload[0];

    if (length == 0)
    {
        return;
    }
    pMqttWorkVar->lastCmd = cmd;

    if (cmd == MQTT_CMD_START_RUN && length == 1 + sizeof(MqttCmdStartRun_t))
    {
        memcpy(&pMqttWorkVar->cmdStartRun, payload + 1, sizeof(MqttCmdStartRun_t));
    }
    else if (cmd == MQTT_CMD_CONNECT || cmd == MQTT_CMD_DISCONNECT || cmd == MQTT_CMD_RESET)
    {
        // recognized command, no payload to extract
    }
    else
    {
        pMqttWorkVar->lastCmd = MQTT_CMD_NONE;
    }
}
