#include "log_mod.h"
#include "log_mod_internal.h"

void LOG_Init(void) {
    Serial.begin(LOG_MODULE_BAUD_RATE);
    delay(LOG_MODULE_INIT_DELAY_MS);
    LOG_INFO(MODULE_LOG, LOG_MODULE_NO_ERROR, "initialized successfully");
}

void LOG_Error(const char* pModule, uint32_t errorCode, const char* pMsg, ...) {
    va_list args;
    va_start(args, pMsg);
    LOG_Print(0, pModule, errorCode, pMsg, args);
    va_end(args);
}

void LOG_Warning(const char* pModule, uint32_t errorCode, const char* pMsg, ...) {
    va_list args;
    va_start(args, pMsg);
    LOG_Print(1, pModule, errorCode, pMsg, args);
    va_end(args);
}

void LOG_Info(const char* pModule, uint32_t errorCode, const char* pMsg, ...) {
    va_list args;
    va_start(args, pMsg);
    LOG_Print(2, pModule, errorCode, pMsg, args);
    va_end(args);
}

static char* LOG_GetLevelString(uint8_t level) {
    switch (level) {
        case 0 : return "ERROR";
        case 1 : return "WARN";
        case 2 : return "INFO";
        default: return "";
    }
}

static void LOG_Print(uint8_t level, const char* pModule, uint32_t errorCode, const char* pMsg, va_list args) {
    Serial.printf("[%8lums]:[%5s]-[%10s]-[%#X]-", millis(), LOG_GetLevelString(level), pModule, errorCode);
    Serial.vprintf(pMsg, args);
    Serial.println();
    Serial.flush(); // Ensure the message is sent before proceeding
}
