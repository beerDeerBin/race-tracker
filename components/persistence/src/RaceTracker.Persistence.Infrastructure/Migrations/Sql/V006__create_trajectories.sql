-- Story 4.3 (/F52/, /D50/): derived 2D dead-reckoning trajectory, computed from the raw samples
-- and stored SEPARATELY so the raw data stays untouched. One row per run sample index; keyed by
-- the same cross-service correlation GUID + runId + index as `samples` (no FK to other services).
-- A plain table (not a hypertable): it is a secondary derived path always queried by exact
-- (device_guid, run_id) + index range, and the PK already gives the ordered index for that read.
CREATE TABLE IF NOT EXISTS trajectory_points (
    device_guid  UUID             NOT NULL,
    run_id       UUID             NOT NULL,
    sample_index BIGINT           NOT NULL,
    t_seconds    DOUBLE PRECISION NOT NULL,  -- t = sample_index / odr_hz
    x            DOUBLE PRECISION NOT NULL,  -- planar position (start fixed at origin)
    y            DOUBLE PRECISION NOT NULL,
    heading      DOUBLE PRECISION NOT NULL,  -- radians, integrated from gz (start 0)
    CONSTRAINT pk_trajectory_points PRIMARY KEY (device_guid, run_id, sample_index)
);
