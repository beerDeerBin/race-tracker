-- Story 8.3 (/F73/, /A60/, /D60/): the transactional notification outbox. A fired rule event is
-- durably recorded here (Pending) so delivery survives a service restart; a background dispatcher
-- pushes each row via SignalR and marks it Dispatched. device_guid is TEXT (not UUID) so the
-- dispatcher pushes to the exact, verbatim SignalR group name (the cross-service correlation key).
CREATE TABLE IF NOT EXISTS notification_outbox (
    id            BIGSERIAL    PRIMARY KEY,
    dedup_key     TEXT         NOT NULL UNIQUE,           -- {type}:{guid}:{firedAt:O} — idempotent enqueue
    device_guid   TEXT         NOT NULL,                  -- /D60/ Fahrzeug-Ref (verbatim group name)
    rule_type     TEXT         NOT NULL,                  -- /D60/ Typ / Auslöser
    message       TEXT         NOT NULL,                  -- /D60/ Meldung
    fired_at      TIMESTAMPTZ  NOT NULL,                  -- /D60/ Zeitstempel
    status        TEXT         NOT NULL DEFAULT 'Pending',-- /D60/ Zustellstatus (Pending | Dispatched)
    created_at    TIMESTAMPTZ  NOT NULL DEFAULT now(),
    dispatched_at TIMESTAMPTZ
);

-- Partial index: the dispatcher only ever scans the pending tail, oldest first.
CREATE INDEX IF NOT EXISTS ix_notification_outbox_pending
    ON notification_outbox (created_at)
    WHERE status = 'Pending';
