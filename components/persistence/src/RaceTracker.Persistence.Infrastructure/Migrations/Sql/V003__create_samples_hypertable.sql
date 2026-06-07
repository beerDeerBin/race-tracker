-- Story 3.1 (/F54/, /D50/): IMU samples as a TimescaleDB hypertable.
-- There is NO timestamp in a sample: the time base is derived as t = sample_index / odr_hz.
-- The hypertable is therefore partitioned on the integer sample_index dimension.
CREATE TABLE IF NOT EXISTS samples (
    device_guid  UUID   NOT NULL,
    run_id       UUID   NOT NULL,
    sample_index BIGINT NOT NULL,
    ax           REAL   NOT NULL,
    ay           REAL   NOT NULL,
    az           REAL   NOT NULL,
    gx           REAL   NOT NULL,
    gy           REAL   NOT NULL,
    gz           REAL   NOT NULL,
    -- The partition column (sample_index) must be part of every unique constraint;
    -- this PK also makes the 3.3 upsert idempotent per (guid, run, index).
    CONSTRAINT pk_samples PRIMARY KEY (device_guid, run_id, sample_index)
);

-- Integer time dimension (no wall-clock column); chunk per 100k sample indices.
SELECT create_hypertable(
    'samples',
    'sample_index',
    chunk_time_interval => 100000,
    if_not_exists => TRUE
);
