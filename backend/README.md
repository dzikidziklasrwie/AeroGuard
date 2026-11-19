# AeroGuard API

The ASP.NET Core API imports simulator-produced JSONL into SQLite and serves
the stored runs to the React dashboard. It never calculates subsystem health or
state transitions. Those decisions remain in the C++ simulator.

Repository: [github.com/dzikidziklasrwie/AeroGuard](https://github.com/dzikidziklasrwie/AeroGuard)

## Run locally

Start from the repository root after generating the files in
`frontend/public/data`:

```bash
dotnet run --project backend/src/AeroGuard.Api
```

The default database is `backend/src/AeroGuard.Api/.data/aeroguard.db`. The
directory is ignored by Git.

Use these environment variables to change the local paths:

| Variable | Purpose |
|---|---|
| `AEROGUARD_DATA_DIR` | Directory containing `manifest.json` and its JSONL files |
| `AEROGUARD_DB_PATH` | SQLite database file |

## Read API

| Method and path | Result |
|---|---|
| `GET /api/health` | API state, database type, and stored run count |
| `GET /api/scenarios` | Schema-v1 manifest with API replay URLs |
| `GET /api/runs` | Runs filtered by scenario or final state |
| `GET /api/runs/{id}` | Metadata and integrity hash for one run |
| `GET /api/runs/{id}/replay` | Original `application/x-ndjson` bytes |
| `GET /api/runs/{id}/records` | Ordered records filtered by type, severity, subsystem, or time |
| `GET /openapi/v1.json` | OpenAPI document |

The importer checks schema version, record order, manifest agreement, safe
base-name paths, UTF-8 input, enum values, and required fields. Each file is
stored in one transaction. A unique SHA-256 value makes repeated startup
imports idempotent. SQL values use parameters, and query limits are bounded.

## Verify

```bash
dotnet restore backend/AeroGuard.Backend.sln
dotnet build backend/AeroGuard.Backend.sln --configuration Release --no-restore
dotnet test backend/AeroGuard.Backend.sln --configuration Release --no-build
```
