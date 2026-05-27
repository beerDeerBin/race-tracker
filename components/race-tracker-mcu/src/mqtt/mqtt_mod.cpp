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

void MQTT_Init(const Guid_t* pGuid) {
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
    if (!mqttClient.setBufferSize(MQTT_MODULE_BUFFER_SIZE)) {
        LOG_ERROR(MODULE_MQTT, MQTT_MODULE_CONNECT_ERROR, "failed to allocate MQTT buffer");
        return;
    }
    mqttClient.setCallback(MQTT_OnMessage);

    LOG_INFO(MODULE_MQTT, MQTT_MODULE_NO_ERROR, "initialized, clientId: %s", pMqttWorkVar->clientId);
}

bool MQTT_Connect(void) {
    bool ok = mqttClient.connect(pMqttWorkVar->clientId, MQTT_BROKER_USER, MQTT_BROKER_PASSWORD, nullptr, 0, false,
                                 nullptr, false);
    if (!ok) {
        LOG_ERROR(MODULE_MQTT, MQTT_MODULE_CONNECT_ERROR, "connection failed, state: %d", mqttClient.state());
        return false;
    }

    if (!mqttClient.subscribe(pMqttWorkVar->cmdTopic, 1)) {
        LOG_WARNING(MODULE_MQTT, MQTT_MODULE_SUBSCRIBE_ERROR, "failed to subscribe to cmd topic");
    }

    LOG_INFO(MODULE_MQTT, MQTT_MODULE_NO_ERROR, "connected, subscribed to %s", pMqttWorkVar->cmdTopic);
    return true;
}

void MQTT_Disconnect(void) {
    mqttClient.disconnect();
    LOG_INFO(MODULE_MQTT, MQTT_MODULE_NO_ERROR, "disconnected");
}

bool MQTT_IsConnected(void) {
    return mqttClient.connected();
}

void MQTT_PublishKeepalive(const MqttStatus_t* pStatus) {
    if (!mqttClient.publish(pMqttWorkVar->statusTopic, (uint8_t*)pStatus, sizeof(MqttStatus_t), false)) {
        LOG_WARNING(MODULE_MQTT, MQTT_MODULE_PUBLISH_ERROR, "keepalive publish failed");
    }
}

void MQTT_PublishBatch(uint32_t runId, uint32_t startOffset) {
    uint32_t count = min(DAMGR_Count(), (uint32_t)MQTT_MODULE_BATCH_MAX);
    if (count == 0) return;

    DAMGR_Pop(pMqttWorkVar->batchBuf, count);

    MqttBatchHeader_t header    = {runId, startOffset, count};
    uint32_t          totalSize = sizeof(MqttBatchHeader_t) + count * sizeof(SampleRecord_t);

    if (!mqttClient.beginPublish(pMqttWorkVar->dataTopic, totalSize, false)) {
        LOG_WARNING(MODULE_MQTT, MQTT_MODULE_PUBLISH_ERROR, "batch publish failed (%lu records)", count);
        return;
    }
    mqttClient.write((uint8_t*)&header, sizeof(header));
    mqttClient.write((uint8_t*)pMqttWorkVar->batchBuf, count * sizeof(SampleRecord_t));
    mqttClient.endPublish();
}

uint8_t MQTT_PollCommand(MqttCmdStartRun_t* pOut) {
    if (!mqttClient.loop()) { LOG_WARNING(MODULE_MQTT, MQTT_MODULE_CONNECT_ERROR, "loop failed, connection lost"); }

    uint8_t cmd = pMqttWorkVar->lastCmd;
    if (cmd == MQTT_CMD_START_RUN && pOut != NULL) {
        memcpy(pOut, &pMqttWorkVar->cmdStartRun, sizeof(MqttCmdStartRun_t));
    }

    pMqttWorkVar->lastCmd = MQTT_CMD_NONE;
    return cmd;
}

static void MQTT_OnMessage(char* topic, uint8_t* payload, unsigned int length) {
    uint8_t cmd = payload[0];

    if (length == 0) return;
    pMqttWorkVar->lastCmd = cmd;

    if (cmd == MQTT_CMD_START_RUN && length == 1 + sizeof(MqttCmdStartRun_t)) {
        memcpy(&pMqttWorkVar->cmdStartRun, payload + 1, sizeof(MqttCmdStartRun_t));
        LOG_INFO(MODULE_MQTT, MQTT_MODULE_NO_ERROR, "received start_run from FE");
    } else if (cmd == MQTT_CMD_CONNECT) {
        LOG_INFO(MODULE_MQTT, MQTT_MODULE_NO_ERROR, "received connect from FE");
    } else if (cmd == MQTT_CMD_DISCONNECT) {
        LOG_INFO(MODULE_MQTT, MQTT_MODULE_NO_ERROR, "received disconnect from FE");
    } else {
        LOG_WARNING(MODULE_MQTT, MQTT_MODULE_NO_ERROR, "received unknown command: 0x%02X", cmd);
        pMqttWorkVar->lastCmd = MQTT_CMD_NONE;
    }
}
