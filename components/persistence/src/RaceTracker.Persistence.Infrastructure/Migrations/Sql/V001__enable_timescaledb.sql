-- Story 3.1: enable the TimescaleDB extension in this database (idempotent).
-- The timescaledb image preloads the library; the extension is still created per-db.
CREATE EXTENSION IF NOT EXISTS timescaledb;
