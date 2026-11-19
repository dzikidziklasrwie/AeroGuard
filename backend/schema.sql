PRAGMA foreign_keys = ON;
PRAGMA journal_mode = WAL;

CREATE TABLE IF NOT EXISTS runs (
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    scenario          TEXT NOT NULL,
    label             TEXT NOT NULL,
    description       TEXT NOT NULL,
    source_file       TEXT NOT NULL,
    source_sha256     TEXT NOT NULL UNIQUE CHECK (length(source_sha256) = 64),
    schema_version    INTEGER NOT NULL CHECK (schema_version = 1),
    seed              INTEGER NOT NULL,
    tick_ms           INTEGER NOT NULL CHECK (tick_ms >= 0),
    duration_ms       INTEGER NOT NULL CHECK (duration_ms >= 0),
    final_state       TEXT NOT NULL,
    record_count      INTEGER NOT NULL CHECK (record_count >= 2),
    imported_at_utc   TEXT NOT NULL,
    replay_bytes      BLOB NOT NULL
);

CREATE TABLE IF NOT EXISTS records (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    run_id         INTEGER NOT NULL,
    ordinal        INTEGER NOT NULL CHECK (ordinal >= 0),
    record_type    TEXT NOT NULL,
    timestamp_ms   INTEGER NOT NULL CHECK (timestamp_ms >= 0),
    severity       TEXT,
    subsystem      TEXT,
    raw_json       TEXT NOT NULL,
    FOREIGN KEY (run_id) REFERENCES runs(id) ON DELETE CASCADE,
    UNIQUE (run_id, ordinal)
);

CREATE INDEX IF NOT EXISTS idx_runs_scenario
    ON runs(scenario, imported_at_utc DESC, id DESC);
CREATE INDEX IF NOT EXISTS idx_runs_final_state
    ON runs(final_state, imported_at_utc DESC, id DESC);
CREATE INDEX IF NOT EXISTS idx_records_type_timestamp
    ON records(run_id, record_type, timestamp_ms, ordinal);
CREATE INDEX IF NOT EXISTS idx_records_severity_timestamp
    ON records(run_id, severity, timestamp_ms, ordinal);
CREATE INDEX IF NOT EXISTS idx_records_subsystem_timestamp
    ON records(run_id, subsystem, timestamp_ms, ordinal);
CREATE INDEX IF NOT EXISTS idx_records_timestamp
    ON records(run_id, timestamp_ms, ordinal);
