#include "wifi_mod.h"
#include "wifi_mod_internal.h"

#include <WiFi.h>
#include <esp_wifi.h>

// Work variables for the Wifi module
static WifiWorkVar_t  wifiWorkVar;
static WifiWorkVar_t* pWifiWorkVar;

/**
 * @brief Initializes the Wi-Fi module by setting up necessary configurations and preparing it for operation. This
 * function should be called before attempting to connect to a Wi-Fi network or perform any Wi-Fi related operations to
 * ensure that the module is properly initialized and ready for use.
 * @return ErrorCode_t indicating the success or failure of the initialization process.
 */
ErrorCode_t WIFI_Init(void)
{
    ErrorCode_t errorCode = NO_ERROR;

    pWifiWorkVar = &wifiWorkVar;
    memset((void*)pWifiWorkVar, 0x00, sizeof(WifiWorkVar_t));

    errorCode |= WIFI_Shutdown();

    return errorCode;
}

/**
 * @brief Connects the Wi-Fi module to a specified network using predefined SSID and password. The function attempts to
 * establish a connection within a specified timeout period, and updates the connection status accordingly. This
 * function should be called after initializing the Wi-Fi module and before attempting to send or receive data over
 * Wi-Fi to ensure that the module is properly connected to the network.
 * @return true if the connection was successful, false otherwise.
 */
bool WIFI_Connect(void)
{
    uint32_t startTs   = millis();
    bool     connected = false;

    WiFi.begin(WIFI_MODULE_SSID, WIFI_MODULE_PASSWORD);

    while (WiFi.status() != WL_CONNECTED && (millis() - startTs) < WIFI_MODULE_CONNECT_TIMEOUT_MS)
    {
        delay(50);
    }

    if (WiFi.status() == WL_CONNECTED)
    {
        pWifiWorkVar->isConnected = true;
    }

    return pWifiWorkVar->isConnected;
}

/**
 * @brief Checks if the Wi-Fi module is currently connected to a network. This function can be used to verify the
 * connection status before attempting to send or receive data over Wi-Fi, ensuring that the module is properly
 * connected and ready for communication.
 * @return true if the Wi-Fi module is connected to a network, false otherwise.
 */
bool WIFI_IsConnected(void)
{
    return pWifiWorkVar->isConnected;
}

/**
 * @brief Shuts down the Wi-Fi module, disconnecting from any connected networks and turning off the Wi-Fi radio to save
 * power. This function should be called when Wi-Fi functionality is no longer needed or before entering deep sleep mode
 * to ensure that the Wi-Fi module is properly powered down.
 * @return ErrorCode_t indicating the success or failure of the operation.
 */
ErrorCode_t WIFI_Shutdown(void)
{
    ErrorCode_t errorCode = NO_ERROR;
    esp_err_t   espErr;

    if (pWifiWorkVar->isConnected)
    {
        if (!WiFi.disconnect(true))
        {
            errorCode |= WIFI_SHUTDOWN_ERROR;
        }
    }

    if (!WiFi.mode(WIFI_OFF))
    {
        errorCode |= WIFI_SHUTDOWN_ERROR;
    }

    espErr = esp_wifi_stop();
    if (espErr != ESP_OK && espErr != ESP_ERR_WIFI_NOT_INIT)
    {
        errorCode |= WIFI_SHUTDOWN_ERROR;
    }

    pWifiWorkVar->isConnected = false;

    return errorCode;
}

/**
 * @brief Wakes up the Wi-Fi module from sleep mode, allowing it to reconnect to the network and resume normal
 * operation.
 * @return ErrorCode_t indicating the success or failure of the operation.
 */
ErrorCode_t WIFI_Wakeup(void)
{
    ErrorCode_t errorCode = NO_ERROR;

    if (!WiFi.mode(WIFI_STA))
    {
        errorCode |= WIFI_WAKEUP_ERROR;
    }

    return errorCode;
}

/**
 * @brief Enables modem sleep mode, which can help reduce power consumption when the Wi-Fi connection is idle, but may
 * increase latency when data needs to be transmitted or received.
 * @return ErrorCode_t indicating the success or failure of the operation.
 */
ErrorCode_t WIFI_EnableModemSleep(void)
{
    ErrorCode_t errorCode = NO_ERROR;

    if (esp_wifi_set_ps(WIFI_PS_MIN_MODEM) != ESP_OK)
    {
        errorCode |= WIFI_SLEEP_ERROR;
    }

    return errorCode;
}

/**
 * @brief Disables modem sleep mode, which can help improve Wi-Fi performance and reduce latency at the cost of
 * increased power consumption.
 * @return ErrorCode_t indicating the success or failure of the operation.
 */
ErrorCode_t WIFI_DisableModemSleep(void)
{
    ErrorCode_t errorCode = NO_ERROR;

    if (esp_wifi_set_ps(WIFI_PS_NONE) != ESP_OK)
    {
        errorCode |= WIFI_SLEEP_ERROR;
    }

    return errorCode;
}
