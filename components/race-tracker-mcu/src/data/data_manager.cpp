#include "data_manager.h"
#include "data_manager_internal.h"

#include <assert.h>
#include <esp_heap_caps.h>

// Work variables for the Data manager
static DataManagerWorkVar_t  dataManagerWorkVar;  // Internal work variables for the Data manager
static DataManagerWorkVar_t* pDataManagerWorkVar; // Pointer to the internal work variables for the Data manager

void DAMGR_Init(void) {
    pDataManagerWorkVar = &dataManagerWorkVar;
    memset((void*)pDataManagerWorkVar, 0x00, sizeof(DataManagerWorkVar_t));

    pDataManagerWorkVar->pRingBuf = (SampleRecord_t*)heap_caps_malloc(DAMGR_PSRAM_SIZE_BYTES, MALLOC_CAP_SPIRAM);
    assert(esp_ptr_external_ram(pDataManagerWorkVar->pRingBuf));
    if (pDataManagerWorkVar->pRingBuf == NULL) {
        LOG_ERROR(MODULE_DAMGR, DAMGR_INIT_ERROR, "failed to allocate ring buffer in PSRAM");
        return;
    }

    memset((void*)pDataManagerWorkVar->pRingBuf, 0x00, DAMGR_PSRAM_SIZE_BYTES);
    pDataManagerWorkVar->mutex = xSemaphoreCreateMutex();

    LOG_INFO(MODULE_DAMGR, DAMGR_NO_ERROR, "initialized successfully");
}

void DAMGR_Reserve(SampleRecord_t** ppOut, uint32_t* pId) {
    xSemaphoreTake(pDataManagerWorkVar->mutex, portMAX_DELAY);

    if ((pDataManagerWorkVar->commitedIdx == 0) && (pDataManagerWorkVar->count < DAMGR_CAPACITY)) {

        pDataManagerWorkVar->commitedIdx = micros();

        (*pId) = pDataManagerWorkVar->commitedIdx;
        *ppOut = &pDataManagerWorkVar->pRingBuf[pDataManagerWorkVar->headIdx];

        pDataManagerWorkVar->headIdx = (pDataManagerWorkVar->headIdx + 1) % DAMGR_CAPACITY;
        pDataManagerWorkVar->count++;
    } else {
        LOG_WARNING(MODULE_DAMGR, DAMGR_OVERFLOW_ERROR, "ring buffer full, cannot reserve new record");
        (*pId) = 0;
        *ppOut = NULL;
    }

    xSemaphoreGive(pDataManagerWorkVar->mutex);
}

void DAMGR_Push(uint32_t id) {
    xSemaphoreTake(pDataManagerWorkVar->mutex, portMAX_DELAY);

    if (id == pDataManagerWorkVar->commitedIdx) {
        pDataManagerWorkVar->commitedIdx = 0;
    } else {
        LOG_WARNING(MODULE_DAMGR, DAMGR_OVERFLOW_ERROR, "invalid record id");
    }

    xSemaphoreGive(pDataManagerWorkVar->mutex);
}

void DAMGR_Pop(SampleRecord_t* pDst, uint32_t numOfRecords) {
    xSemaphoreTake(pDataManagerWorkVar->mutex, portMAX_DELAY);

    if (pDataManagerWorkVar->count < numOfRecords) {
        LOG_WARNING(MODULE_DAMGR, DAMGR_NO_ERROR, "ring buffer underflow");
        xSemaphoreGive(pDataManagerWorkVar->mutex);
        return;
    }

    memcpy(pDst, &pDataManagerWorkVar->pRingBuf[pDataManagerWorkVar->tailIdx], numOfRecords * sizeof(SampleRecord_t));
    pDataManagerWorkVar->tailIdx  = (pDataManagerWorkVar->tailIdx + numOfRecords) % DAMGR_CAPACITY;
    pDataManagerWorkVar->count   -= numOfRecords;

    xSemaphoreGive(pDataManagerWorkVar->mutex);
}

uint32_t DAMGR_Count(void) {
    xSemaphoreTake(pDataManagerWorkVar->mutex, portMAX_DELAY);
    uint32_t count = pDataManagerWorkVar->count;
    xSemaphoreGive(pDataManagerWorkVar->mutex);
    return count;
}

void DAMGR_Clear(void) {
    xSemaphoreTake(pDataManagerWorkVar->mutex, portMAX_DELAY);
    pDataManagerWorkVar->headIdx     = 0;
    pDataManagerWorkVar->tailIdx     = 0;
    pDataManagerWorkVar->count       = 0;
    pDataManagerWorkVar->commitedIdx = 0;
    xSemaphoreGive(pDataManagerWorkVar->mutex);
}
