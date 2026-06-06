-- Story 4.2 (/F53/, /L40/): prerequisite for a continuous aggregate over an integer-time
-- hypertable. `samples` is partitioned on the integer `sample_index` (no wall-clock column, /F54/),
-- so TimescaleDB needs an "integer now" function before a continuous aggregate can be created on it.
-- The roll-up's time dimension is sample-index space, so "now" is simply the highest stored index;
-- everything up to it is materialisable, anything beyond is served live by real-time aggregation.
CREATE OR REPLACE FUNCTION samples_integer_now() RETURNS BIGINT
    LANGUAGE SQL STABLE
    AS $$ SELECT coalesce(max(sample_index), 0)::bigint FROM samples $$;

SELECT set_integer_now_func('samples', 'samples_integer_now', replace_if_exists => true);
