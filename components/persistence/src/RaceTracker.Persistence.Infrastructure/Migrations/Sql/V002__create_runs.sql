-- Story 3.1 (/F55/, /D40/): per-run metadata. Low volume -> a plain table.
-- Keyed by the cross-service correlation GUID + runId (no FK to other services' stores).
CREATE TABLE IF NOT EXISTS runs (
    device_guid      UUID        NOT NULL,
    run_id           UUID        NOT NULL,
    num_samples      INTEGER,
    odr_hz           INTEGER,
    accel_range      SMALLINT,
    gyro_range       SMALLINT,
    started_at       TIMESTAMPTZ,
    ended_at         TIMESTAMPTZ,
    received_samples INTEGER     NOT NULL DEFAULT 0,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at       TIMESTAMPTZ,
    CONSTRAINT pk_runs PRIMARY KEY (device_guid, run_id)
);
