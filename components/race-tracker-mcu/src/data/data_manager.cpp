#include "data_manager.h"
#include "data_manager_internal.h"

#include <assert.h>
#include <esp_heap_caps.h>

// Work variables for the Data manager
static DataManagerWorkVar_t  dataManagerWorkVar;
static DataManagerWorkVar_t* pDataManagerWorkVar;

/**
 * @brief Initializes the data manager by allocating the sample ring buffer in PSRAM and creating the mutex that guards
 * concurrent access. This function must be called once at startup before any other data manager function is used.
 * @return ErrorCode_t indicating the success or failure of the initialization process.
 */
ErrorCode_t DAMGR_Init(void)
{
    pDataManagerWorkVar = &dataManagerWorkVar;
    memset((void*)pDataManagerWorkVar, 0x00, sizeof(DataManagerWorkVar_t));

    pDataManagerWorkVar->pRingBuf = (SampleRecord_t*)heap_caps_malloc(DAMGR_PSRAM_SIZE_BYTES, MALLOC_CAP_SPIRAM);
    assert(esp_ptr_external_ram(pDataManagerWorkVar->pRingBuf));
    if (pDataManagerWorkVar->pRingBuf == NULL)
    {
        return DAMGR_ALLOC_ERROR;
    }

    memset((void*)pDataManagerWorkVar->pRingBuf, 0x00, DAMGR_PSRAM_SIZE_BYTES);
    pDataManagerWorkVar->mutex = xSemaphoreCreateMutex();

    return NO_ERROR;
}

/**
 * @brief Reserves the next free slot in the ring buffer and hands back a pointer to it together with a unique
 * reservation id. The caller fills the record and then commits it with DAMGR_Push using the returned id. On failure (no
 * free slot, or a reservation is already pending) the output pointer is set to NULL and the id to 0.
 * @param ppOut Output pointer that receives the address of the reserved record, or NULL on failure.
 * @param pId Output pointer that receives the unique reservation id, or 0 on failure.
 */
void DAMGR_Reserve(SampleRecord_t** ppOut, uint32_t* pId)
{
    xSemaphoreTake(pDataManagerWorkVar->mutex, portMAX_DELAY);

    if ((pDataManagerWorkVar->commitedIdx == 0) && (pDataManagerWorkVar->count < DAMGR_CAPACITY))
    {
        pDataManagerWorkVar->commitedIdx = micros();

        (*pId) = pDataManagerWorkVar->commitedIdx;
        *ppOut = &pDataManagerWorkVar->pRingBuf[pDataManagerWorkVar->headIdx];

        pDataManagerWorkVar->headIdx = (pDataManagerWorkVar->headIdx + 1) % DAMGR_CAPACITY;
        pDataManagerWorkVar->count++;
    }
    else
    {
        (*pId) = 0;
        *ppOut = NULL;
    }

    xSemaphoreGive(pDataManagerWorkVar->mutex);
}

/**
 * @brief Commits a previously reserved record, making it available to read. The id must match the one returned by the
 * corresponding DAMGR_Reserve call.
 * @param id The reservation id returned by DAMGR_Reserve.
 * @return ErrorCode_t indicating the success or failure of the commit.
 */
ErrorCode_t DAMGR_Push(uint32_t id)
{
    ErrorCode_t errorCode = NO_ERROR;

    xSemaphoreTake(pDataManagerWorkVar->mutex, portMAX_DELAY);

    if (id == pDataManagerWorkVar->commitedIdx)
    {
        pDataManagerWorkVar->commitedIdx = 0;
    }
    else
    {
        errorCode = DAMGR_OVERFLOW_ERROR;
    }

    xSemaphoreGive(pDataManagerWorkVar->mutex);
    return errorCode;
}

/**
 * @brief Copies the requested number of records from the tail of the ring buffer into the destination buffer and
 * advances the tail. Fails if fewer than numOfRecords records are currently buffered.
 * @param pDst Destination buffer that receives the popped records. Must hold at least numOfRecords entries.
 * @param numOfRecords Number of records to pop.
 * @return ErrorCode_t indicating the success or failure of the operation.
 */
ErrorCode_t DAMGR_Pop(SampleRecord_t* pDst, uint32_t numOfRecords)
{
    xSemaphoreTake(pDataManagerWorkVar->mutex, portMAX_DELAY);

    if (pDataManagerWorkVar->count < numOfRecords)
    {
        xSemaphoreGive(pDataManagerWorkVar->mutex);
        return DAMGR_OVERFLOW_ERROR;
    }

    memcpy(pDst, &pDataManagerWorkVar->pRingBuf[pDataManagerWorkVar->tailIdx], numOfRecords * sizeof(SampleRecord_t));
    pDataManagerWorkVar->tailIdx  = (pDataManagerWorkVar->tailIdx + numOfRecords) % DAMGR_CAPACITY;
    pDataManagerWorkVar->count   -= numOfRecords;

    xSemaphoreGive(pDataManagerWorkVar->mutex);
    return NO_ERROR;
}

/**
 * @brief Returns the number of records currently buffered and available to pop.
 * @return The number of records in the ring buffer.
 */
uint32_t DAMGR_Count(void)
{
    xSemaphoreTake(pDataManagerWorkVar->mutex, portMAX_DELAY);
    uint32_t count = pDataManagerWorkVar->count;
    xSemaphoreGive(pDataManagerWorkVar->mutex);
    return count;
}

/**
 * @brief Resets the ring buffer to empty, discarding any buffered records and clearing any pending reservation.
 */
void DAMGR_Clear(void)
{
    xSemaphoreTake(pDataManagerWorkVar->mutex, portMAX_DELAY);
    pDataManagerWorkVar->headIdx     = 0;
    pDataManagerWorkVar->tailIdx     = 0;
    pDataManagerWorkVar->count       = 0;
    pDataManagerWorkVar->commitedIdx = 0;
    xSemaphoreGive(pDataManagerWorkVar->mutex);
}
