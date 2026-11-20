# AeroGuard API

The ASP.NET Core API imports simulator JSONL into SQLite, serves archived
runs, and owns local accounts, roles, bearer sessions, aircraft records,
assignments, simulated active flights, flight chat, and the audit log. It
selects stored telemetry for the operations map but never evaluates fictional
sensor thresholds or state transitions. Those decisions remain in the C++
simulator.

Repository: [github.com/dzikidziklasrwie/AeroGuard](https://github.com/dzikidziklasrwie/AeroGuard)

## Start locally

Generate or retain the files in `frontend/public/data`, set a bootstrap
password outside Git, then start the API from the repository root:

PowerShell:

```powershell
$env:AEROGUARD_ADMIN_PASSWORD = "Choose-A-Long-Local-Password-42!"
dotnet run --project backend/src/AeroGuard.Api
```

Bash:

```bash
export AEROGUARD_ADMIN_PASSWORD='Choose-A-Long-Local-Password-42!'
dotnet run --project backend/src/AeroGuard.Api
```

The API creates `admin` on first start and stores a password hash, never the
plain password. Direct API starts use one-time bootstrap behaviour by default.
Set `AEROGUARD_SYNC_ADMIN_PASSWORD=true` to make the runtime value update an
existing configured administrator and revoke that account's active sessions.
The Compose stack enables this option so changes to its local `.env` take
effect after the API container restarts.

The default database is `backend/src/AeroGuard.Api/.data/aeroguard.db`. Local
database files and environment files are ignored by Git.

| Variable | Purpose | Default |
|---|---|---|
| `AEROGUARD_ADMIN_USERNAME` | Initial administrator username | `admin` |
| `AEROGUARD_ADMIN_PASSWORD` | Initial administrator password | No value; no account is created without it |
| `AEROGUARD_SYNC_ADMIN_PASSWORD` | Synchronize the configured password on startup and revoke older sessions | `false`; Compose sets `true` |
| `AEROGUARD_ALLOW_WEAK_DEVELOPMENT_PASSWORD` | Permit a short local-only password in Development or Testing | `false`; local Compose sets `true` |
| `AEROGUARD_SESSION_HOURS` | Bearer-session lifetime from 1 to 168 hours | `8` |
| `AEROGUARD_DATA_DIR` | Directory containing `manifest.json` and JSONL files | `frontend/public/data` resolved from the repository |
| `AEROGUARD_DB_PATH` | SQLite database file | API `.data` directory |
| `AEROGUARD_TRUST_PROXY_HEADERS` | Accept one proxy hop's forwarded client address and scheme | `false`; Compose sets `true` for its nginx proxy |

For an explicit local demonstration of `admin` / `admin`, run only in the
Development environment and enable the weak-password switch:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:AEROGUARD_ALLOW_WEAK_DEVELOPMENT_PASSWORD = "true"
$env:AEROGUARD_ADMIN_PASSWORD = "admin"
dotnet run --project backend/src/AeroGuard.Api
```

Never use that switch for a shared or public deployment. It is rejected in
Production even if the variable is set.

The supplied Compose file is a loopback-only development stack. It enables
this switch so a deliberate local `admin` / `admin` setting works, but the
template still supplies a longer example password. Replace it before use.

## Route groups

### Public replay archive

| Method and path | Result |
|---|---|
| `GET /api/health` | API state, database type, and stored run count |
| `GET /api/scenarios` | Newest archived run for each supplied scenario |
| `GET /api/runs` | Runs filtered by scenario or final state |
| `GET /api/runs/{id}` | Metadata and SHA-256 value for one run |
| `GET /api/runs/{id}/replay` | Original `application/x-ndjson` bytes |
| `GET /api/runs/{id}/records` | Ordered records filtered by type, severity, subsystem, or time |
| `GET /openapi/v1.json` | OpenAPI document |

### Access

| Method and path | Access | Result |
|---|---|---|
| `POST /api/auth/login` | Public, rate-limited | Create an opaque bearer session |
| `GET /api/auth/me` | Authenticated | Read the current account and roles |
| `POST /api/auth/logout` | Authenticated | Revoke the current session |

Send protected requests with `Authorization: Bearer <access_token>`. Tokens
are returned once at login. SQLite stores only their SHA-256 values. Logout,
password reset, and account deactivation revoke affected sessions.

### Administration

All `/api/admin` routes require the built-in `Admin` role.

| Method and path | Result |
|---|---|
| `GET`, `POST /api/admin/users` | List or create users |
| `PUT /api/admin/users/{id}/roles` | Replace assigned roles |
| `PUT /api/admin/users/{id}/status` | Activate or deactivate a user |
| `PUT /api/admin/users/{id}/password` | Set a password and revoke sessions |
| `GET /api/admin/users/{id}/permissions` | Read effective operation permissions |
| `PUT /api/admin/users/{id}/permissions` | Replace the account's complete operation-permission override |
| `GET`, `POST /api/admin/roles` | List or create roles |
| `DELETE /api/admin/roles/{id}` | Delete an unused custom role |
| `GET /api/admin/audit?limit=50` | Read recent access, fleet, and operations changes |

The API prevents removal or deactivation of the final active administrator.
Built-in roles cannot be deleted.

### Aircraft

Every `/api/aircraft` route requires a bearer session. Administrators see all
aircraft. Other accounts see only assigned records.

| Method and path | Access | Result |
|---|---|---|
| `GET /api/aircraft` | Authenticated | List visible aircraft |
| `GET /api/aircraft/{id}` | Assigned or Admin | Read one aircraft |
| `POST /api/aircraft` | Operator or Admin | Create an aircraft; creator becomes `OWNER` |
| `PUT /api/aircraft/{id}` | Admin, `OWNER`, or `MAINTAINER` | Update with `expected_version` |
| `DELETE /api/aircraft/{id}` | Admin | Delete an aircraft after removing any active map flight |
| `GET /api/aircraft/{id}/assignments` | Admin | List account assignments |
| `PUT`, `DELETE /api/aircraft/{id}/assignments/{userId}` | Admin | Set or remove an assignment |

Assignment levels are `OWNER`, `MAINTAINER`, and `VIEWER`. At least one owner
must remain. Version checks reject stale concurrent updates with HTTP 409.

### Simulated operations

Every `/api/operations` route and the `/hubs/operations` SignalR hub require an
active bearer session.

| Method and path | Access | Result |
|---|---|---|
| `GET /api/operations/permissions` | Authenticated | Read the current account's effective operation permissions |
| `GET /api/operations/flights` | Admin or assigned account | List visible active simulated flights |
| `POST /api/operations/flights` | Admin, or `OWNER` with `FLIGHT_PLACE_OWN` | Place an `ACTIVE` aircraft on the map |
| `PUT /api/operations/flights/{id}` | Admin, or `OWNER` with `FLIGHT_RECONFIGURE_OWN` | Change route or archived run with `expected_version` |
| `DELETE /api/operations/flights/{id}` | Admin, or `OWNER` with `FLIGHT_REMOVE_OWN` | Remove the flight and its chat rows |
| `GET /api/operations/flights/{id}/messages` | Admin or assigned account | Read 1 to 200 recent chat rows |
| `POST /api/operations/flights/{id}/messages` | Admin, or assigned account with `CHAT_SEND` | Send text or current location; telemetry also needs `OWNER` and `TELEMETRY_SHARE` |
| `/hubs/operations` | Authenticated | Receive authorized flight and message updates |

`Admin` bypasses the five operation checks. With no override rows, `Operator`
gets all five permissions and `Viewer` gets `CHAT_SEND`. A custom-role-only
account gets none. Once an administrator saves an override, that complete set
replaces role defaults; an empty set denies all five permissions.

Any active assigned user can view an aircraft's flight. The map's primary
owner label uses the earliest `OWNER` assignment by assignment time, then user
ID. Assignment routes return the complete co-owner list.

Placement requires one archived run and an `ACTIVE` aircraft that has no other
active flight. The aircraft must remain `ACTIVE` until that flight is removed;
the API also rejects aircraft deletion while it is mapped. Latitude accepts
-85 to 85, longitude -180 to 180, and speed 400 to 1100 km/h. `WORLD` is the
canonical world-scope code. Continent routes use `AF`, `AN`, `AS`, `EU`, `NA`,
`OC`, or `SA`; country routes use ISO 3166-1 alpha-3 codes.

The API broadcasts a nominal update each second. Synthetic route motion uses
an accelerated display clock. The archived scenario telemetry advances at
wall-clock speed and loops after its 40 to 55 second recorded duration.

SignalR accepts `access_token` in the query only under `/hubs/operations`, as
required by browser WebSocket transport. nginx disables access logging for the
hub path. A process-local registry stores connection, user, and session IDs;
the broadcaster rechecks the session before delivery. This supports one API
instance. Multiple replicas need shared connection coordination and a SignalR
backplane.

Fixed-window limits allow 30 login requests per source address per minute, 90
flight writes and 60 chat writes per account per minute, and 30 hub-negotiation
requests per account per minute. Authentication runs before account-based
rate-limit partitioning.

In the Compose stack, the API's direct host port is bound to
`127.0.0.1:8081`; nginx remains the browser entry point on port 8080. Compose
enables one-hop forwarded-header processing so login limits use the client
address supplied by that nginx proxy. Don't enable forwarded-header trust for
an untrusted direct connection.

## Storage and validation

The startup importer checks schema version, record order, manifest agreement,
safe base-name paths, UTF-8 input, enum values, and required fields. Each
artifact is stored in one transaction. A unique SHA-256 value makes repeated
startup imports idempotent. SQL values use parameters, and query limits are
bounded.

Account, fleet, and operations services validate identifiers, passwords,
roles, statuses, tail numbers, route values, string lengths, and permissions
before repository calls. Active flights, account operation permissions, and
chat rows share the same SQLite database. Each flight keeps its latest 500 chat
rows until flight deletion; one read returns at most 200. API errors use
problem details with a request trace identifier. Chat permission predicates
and insertion run in one SQL statement so a concurrent revocation cannot leave
an unauthorized row.

The broadcaster logs a failed flight update and continues with other flights.
Its connection registry is process memory and it builds snapshots per
recipient, which suits a small single-instance demonstration rather than a
distributed or high-volume fleet.

## Verify

```bash
dotnet restore backend/AeroGuard.Backend.sln
dotnet build backend/AeroGuard.Backend.sln --configuration Release --no-restore
dotnet test backend/AeroGuard.Backend.sln --configuration Release --no-build
dotnet format backend/AeroGuard.Backend.sln --verify-no-changes --no-restore
```

The integration suite uses a new temporary SQLite database, imports all seven
replays, bootstraps a test administrator, and exercises public and protected
routes through the in-process HTTP server. The current local snapshot passes
48/48 xUnit tests, including administrator password synchronization, flight motion, map authorization, country and
polar route bounds, active-flight status, chat bounds, permission overrides,
hub token scope, and revoked-session recipient checks.
