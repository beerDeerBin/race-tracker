-- timescaledb:no-transaction
-- Story 4.2 (/F53/, /L40/): continuous aggregate roll-up over the samples hypertable.
-- Buckets the integer time dimension (sample_index) into fixed 100-index windows and
-- pre-computes min/max/avg per IMU axis so dashboards query summaries, not raw rows.
-- TimescaleDB forbids creating a continuous aggregate inside a transaction block, hence the
-- no-transaction marker above (V004 first sets the required integer-now function). Real-time
-- aggregation stays ON (materialized_only = false): queries merge the materialized region with an
-- on-the-fly aggregation of not-yet-materialized samples, so results are always complete even
-- before a refresh. IF NOT EXISTS keeps it idempotent.
CREATE MATERIALIZED VIEW IF NOT EXISTS samples_rollup
WITH (timescaledb.continuous, timescaledb.materialized_only = false) AS
SELECT
    device_guid,
    run_id,
    time_bucket(100, sample_index) AS bucket_start,
    min(ax) AS ax_min, max(ax) AS ax_max, avg(ax) AS ax_avg,
    min(ay) AS ay_min, max(ay) AS ay_max, avg(ay) AS ay_avg,
    min(az) AS az_min, max(az) AS az_max, avg(az) AS az_avg,
    min(gx) AS gx_min, max(gx) AS gx_max, avg(gx) AS gx_avg,
    min(gy) AS gy_min, max(gy) AS gy_max, avg(gy) AS gy_avg,
    min(gz) AS gz_min, max(gz) AS gz_max, avg(gz) AS gz_avg,
    count(*)::bigint AS sample_count
FROM samples
GROUP BY device_guid, run_id, bucket_start
WITH NO DATA;
