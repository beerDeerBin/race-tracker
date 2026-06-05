#pragma once

// Internal work variables for the Data manager
typedef struct
{
    SampleRecord_t*   pRingBuf;    // Ring buffer for storing sample records
    uint32_t          count;       // Number of records currently in the ring buffer
    uint32_t          headIdx;     // Index of the head of the ring buffer
    uint32_t          tailIdx;     // Index of the tail of the ring buffer
    uint32_t          commitedIdx; // Index of the last commited record in the ring buffer
    SemaphoreHandle_t mutex;       // Mutex for synchronizing access to the ring buffer
} DataManagerWorkVar_t;
