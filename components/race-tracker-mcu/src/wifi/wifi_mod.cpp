#include "wifi_mod.h"
#include "wifi_mod_internal.h"

#include <WiFi.h>
#include <esp_wifi.h>

// Work variables for the Wifi module
static WifiWorkVar_t  wifiWorkVar;
static WifiWorkVar_t* pWifiWorkVar;

void WIFI_Init(void) {
    pWifiWorkVar = &wifiWorkVar;
    memset((void*)pWifiWorkVar, 0x00, sizeof(WifiWorkVar_t));

    WIFI_Shutdown();

    LOG_INFO(MODULE_WIFI, WIFI_MODULE_NO_ERROR, "initialized");
}

bool WIFI_Connect(void) {
    WiFi.begin(WIFI_MODULE_SSID, WIFI_MODULE_PASSWORD);

    uint32_t t0 = millis();
    while (WiFi.status() != WL_CONNECTED && (millis() - t0) < WIFI_MODULE_CONNECT_TIMEOUT_MS) { delay(100); }

    if (WiFi.status() == WL_CONNECTED) {
        pWifiWorkVar->isConnected = true;
        LOG_INFO(MODULE_WIFI, WIFI_MODULE_NO_ERROR, "connected, IP: %s", WiFi.localIP().toString().c_str());
        return true;
    }

    WiFi.disconnect(true);
    LOG_ERROR(MODULE_WIFI, WIFI_MODULE_CONNECT_ERROR, "connection timed out");
    return false;
}

bool WIFI_IsConnected(void) {
    return pWifiWorkVar->isConnected;
}

void WIFI_Shutdown(void) {
    if (pWifiWorkVar->isConnected) {
        if (!WiFi.disconnect(true)) { LOG_WARNING(MODULE_WIFI, WIFI_MODULE_SHUTDOWN_ERROR, "disconnect failed"); }
    }
    if (!WiFi.mode(WIFI_OFF)) {
        LOG_ERROR(MODULE_WIFI, WIFI_MODULE_SHUTDOWN_ERROR, "failed to set radio off");
        return;
    }

    esp_err_t err = esp_wifi_stop();
    if (err != ESP_OK && err != ESP_ERR_WIFI_NOT_INIT) {
        LOG_WARNING(MODULE_WIFI, WIFI_MODULE_SHUTDOWN_ERROR, "esp_wifi_stop failed (err: %d)", err);
    }

    pWifiWorkVar->isConnected = false;
    LOG_INFO(MODULE_WIFI, WIFI_MODULE_NO_ERROR, "radio off");
}

void WIFI_Wakeup(void) {
    if (!WiFi.mode(WIFI_STA)) {
        LOG_ERROR(MODULE_WIFI, WIFI_MODULE_WAKEUP_ERROR, "failed to set STA mode");
        return;
    }

    WIFI_DisableModemSleep();

    LOG_INFO(MODULE_WIFI, WIFI_MODULE_NO_ERROR, "radio on");
}

void WIFI_EnableModemSleep(void) {
    esp_err_t err = esp_wifi_set_ps(WIFI_PS_MIN_MODEM);
    if (err != ESP_OK) {
        LOG_ERROR(MODULE_WIFI, WIFI_MODULE_SLEEP_ERROR, "failed to enable modem sleep (err: %d)", err);
        return;
    }
    LOG_INFO(MODULE_WIFI, WIFI_MODULE_NO_ERROR, "modem sleep enabled");
}

void WIFI_DisableModemSleep(void) {
    esp_err_t err = esp_wifi_set_ps(WIFI_PS_NONE);
    if (err != ESP_OK) {
        LOG_ERROR(MODULE_WIFI, WIFI_MODULE_SLEEP_ERROR, "failed to disable modem sleep (err: %d)", err);
        return;
    }
    LOG_INFO(MODULE_WIFI, WIFI_MODULE_NO_ERROR, "modem sleep disabled");
}
