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

CREATE TABLE IF NOT EXISTS roles (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    name            TEXT NOT NULL COLLATE NOCASE UNIQUE
                        CHECK (length(name) BETWEEN 2 AND 40),
    description     TEXT NOT NULL CHECK (length(description) <= 200),
    is_system       INTEGER NOT NULL DEFAULT 0 CHECK (is_system IN (0, 1)),
    created_at_utc  TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS users (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    username        TEXT NOT NULL COLLATE NOCASE UNIQUE
                        CHECK (length(username) BETWEEN 3 AND 64),
    display_name    TEXT NOT NULL CHECK (length(display_name) BETWEEN 1 AND 100),
    password_hash   TEXT NOT NULL,
    is_active       INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
    created_at_utc  TEXT NOT NULL,
    updated_at_utc  TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS user_roles (
    user_id     INTEGER NOT NULL,
    role_id     INTEGER NOT NULL,
    assigned_at_utc TEXT NOT NULL,
    PRIMARY KEY (user_id, role_id),
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    FOREIGN KEY (role_id) REFERENCES roles(id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS sessions (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    token_hash      TEXT NOT NULL UNIQUE CHECK (length(token_hash) = 64),
    user_id         INTEGER NOT NULL,
    created_at_utc  TEXT NOT NULL,
    expires_at_utc  TEXT NOT NULL,
    revoked_at_utc  TEXT,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS aircraft (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    tail_number     TEXT NOT NULL COLLATE NOCASE UNIQUE
                        CHECK (length(tail_number) BETWEEN 2 AND 16),
    display_name    TEXT NOT NULL CHECK (length(display_name) BETWEEN 1 AND 100),
    model           TEXT NOT NULL CHECK (length(model) BETWEEN 1 AND 100),
    status          TEXT NOT NULL
                        CHECK (status IN ('ACTIVE', 'MAINTENANCE', 'GROUNDED')),
    notes           TEXT NOT NULL DEFAULT '' CHECK (length(notes) <= 1000),
    version         INTEGER NOT NULL DEFAULT 1 CHECK (version >= 1),
    created_by      INTEGER NOT NULL,
    created_at_utc  TEXT NOT NULL,
    updated_at_utc  TEXT NOT NULL,
    FOREIGN KEY (created_by) REFERENCES users(id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS aircraft_assignments (
    aircraft_id     INTEGER NOT NULL,
    user_id         INTEGER NOT NULL,
    access_level    TEXT NOT NULL
                        CHECK (access_level IN ('OWNER', 'MAINTAINER', 'VIEWER')),
    assigned_by     INTEGER NOT NULL,
    assigned_at_utc TEXT NOT NULL,
    PRIMARY KEY (aircraft_id, user_id),
    FOREIGN KEY (aircraft_id) REFERENCES aircraft(id) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    FOREIGN KEY (assigned_by) REFERENCES users(id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS audit_log (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    actor_user_id   INTEGER,
    action          TEXT NOT NULL CHECK (length(action) BETWEEN 3 AND 80),
    entity_type     TEXT NOT NULL CHECK (length(entity_type) BETWEEN 2 AND 40),
    entity_id       TEXT NOT NULL CHECK (length(entity_id) BETWEEN 1 AND 80),
    details_json    TEXT NOT NULL DEFAULT '{}',
    occurred_at_utc TEXT NOT NULL,
    FOREIGN KEY (actor_user_id) REFERENCES users(id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_user_roles_role
    ON user_roles(role_id, user_id);
CREATE INDEX IF NOT EXISTS idx_sessions_user_expiry
    ON sessions(user_id, expires_at_utc);
CREATE INDEX IF NOT EXISTS idx_aircraft_assignments_user
    ON aircraft_assignments(user_id, aircraft_id);
CREATE INDEX IF NOT EXISTS idx_aircraft_status
    ON aircraft(status, tail_number);
CREATE INDEX IF NOT EXISTS idx_audit_log_occurred
    ON audit_log(occurred_at_utc DESC, id DESC);

CREATE TRIGGER IF NOT EXISTS protect_last_active_admin_role
BEFORE DELETE ON user_roles
WHEN EXISTS (
        SELECT 1
        FROM roles
        WHERE roles.id = OLD.role_id
          AND roles.name = 'Admin' COLLATE NOCASE
    )
    AND EXISTS (
        SELECT 1
        FROM users
        WHERE users.id = OLD.user_id
          AND users.is_active = 1
    )
    AND (
        SELECT COUNT(DISTINCT users.id)
        FROM users
        JOIN user_roles ON user_roles.user_id = users.id
        JOIN roles ON roles.id = user_roles.role_id
        WHERE users.is_active = 1
          AND roles.name = 'Admin' COLLATE NOCASE
    ) <= 1
BEGIN
    SELECT RAISE(ABORT, 'the final active administrator must keep the Admin role');
END;

CREATE TRIGGER IF NOT EXISTS protect_last_active_admin_status
BEFORE UPDATE OF is_active ON users
WHEN OLD.is_active = 1
    AND NEW.is_active = 0
    AND EXISTS (
        SELECT 1
        FROM user_roles
        JOIN roles ON roles.id = user_roles.role_id
        WHERE user_roles.user_id = OLD.id
          AND roles.name = 'Admin' COLLATE NOCASE
    )
    AND (
        SELECT COUNT(DISTINCT users.id)
        FROM users
        JOIN user_roles ON user_roles.user_id = users.id
        JOIN roles ON roles.id = user_roles.role_id
        WHERE users.is_active = 1
          AND roles.name = 'Admin' COLLATE NOCASE
    ) <= 1
BEGIN
    SELECT RAISE(ABORT, 'the final active administrator cannot be deactivated');
END;

CREATE TRIGGER IF NOT EXISTS protect_last_aircraft_owner_delete
BEFORE DELETE ON aircraft_assignments
WHEN OLD.access_level = 'OWNER'
    AND EXISTS (
        SELECT 1 FROM aircraft WHERE aircraft.id = OLD.aircraft_id
    )
    AND (
        SELECT COUNT(*)
        FROM aircraft_assignments
        WHERE aircraft_id = OLD.aircraft_id
          AND access_level = 'OWNER'
    ) <= 1
BEGIN
    SELECT RAISE(ABORT, 'the final aircraft owner cannot be removed');
END;

CREATE TRIGGER IF NOT EXISTS protect_last_aircraft_owner_update
BEFORE UPDATE OF access_level ON aircraft_assignments
WHEN OLD.access_level = 'OWNER'
    AND NEW.access_level <> 'OWNER'
    AND (
        SELECT COUNT(*)
        FROM aircraft_assignments
        WHERE aircraft_id = OLD.aircraft_id
          AND access_level = 'OWNER'
    ) <= 1
BEGIN
    SELECT RAISE(ABORT, 'the final aircraft owner cannot be changed');
END;

INSERT OR IGNORE INTO roles (name, description, is_system, created_at_utc)
VALUES
    ('Admin', 'Manages accounts, roles, aircraft, and assignments.', 1, strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    ('Operator', 'Creates aircraft and updates assigned aircraft.', 1, strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    ('Viewer', 'Reads aircraft assigned to the account.', 1, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
