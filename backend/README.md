# AeroGuard API

The ASP.NET Core API imports simulator JSONL into SQLite, serves archived
runs, and owns local accounts, roles, bearer sessions, aircraft records,
assignments, and the audit log. It never calculates subsystem health or state
transitions. Those decisions remain in the C++ simulator.

Repository: [github.com/dzikidziklasrwie/AeroGuard](https://github.com/dzikidziklasrwie/AeroGuard)

## Start locally

Generate or retain the files in `frontend/public/data`, set a bootstrap
password outside Git, then start the API from the repository root:

PowerShell:

```powershell
$env:AEROGUARD_ADMIN_PASSWORD = "choose-a-long-local-password"
dotnet run --project backend/src/AeroGuard.Api
```

Bash:

```bash
export AEROGUARD_ADMIN_PASSWORD='choose-a-long-local-password'
dotnet run --project backend/src/AeroGuard.Api
```

The API creates `admin` on first start and stores a password hash, never the
plain password. If an administrator already exists, changing the environment
variable does not reset that account. Use the password-reset endpoint or start
with a new local database.

The default database is `backend/src/AeroGuard.Api/.data/aeroguard.db`. Local
database files and environment files are ignored by Git.

| Variable | Purpose | Default |
|---|---|---|
| `AEROGUARD_ADMIN_USERNAME` | Initial administrator username | `admin` |
| `AEROGUARD_ADMIN_PASSWORD` | Initial administrator password | No value; no account is created without it |
| `AEROGUARD_SESSION_HOURS` | Bearer-session lifetime from 1 to 168 hours | `8` |
| `AEROGUARD_DATA_DIR` | Directory containing `manifest.json` and JSONL files | `frontend/public/data` resolved from the repository |
| `AEROGUARD_DB_PATH` | SQLite database file | API `.data` directory |

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
| `GET`, `POST /api/admin/roles` | List or create roles |
| `DELETE /api/admin/roles/{id}` | Delete an unused custom role |
| `GET /api/admin/audit?limit=50` | Read recent access and fleet changes |

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
| `DELETE /api/aircraft/{id}` | Admin | Delete an aircraft |
| `GET /api/aircraft/{id}/assignments` | Admin | List account assignments |
| `PUT`, `DELETE /api/aircraft/{id}/assignments/{userId}` | Admin | Set or remove an assignment |

Assignment levels are `OWNER`, `MAINTAINER`, and `VIEWER`. At least one owner
must remain. Version checks reject stale concurrent updates with HTTP 409.

## Storage and validation

The startup importer checks schema version, record order, manifest agreement,
safe base-name paths, UTF-8 input, enum values, and required fields. Each
artifact is stored in one transaction. A unique SHA-256 value makes repeated
startup imports idempotent. SQL values use parameters, and query limits are
bounded.

Account and fleet services validate identifiers, passwords, roles, statuses,
tail numbers, string lengths, and permissions before repository calls. API
errors use problem details with a request trace identifier.

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
31/31 xUnit integration tests.
