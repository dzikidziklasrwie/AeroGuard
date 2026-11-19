# AeroGuard user manual

## 1. Purpose and scope

AeroGuard demonstrates health monitoring for six fictional aircraft
subsystems. A C++20 application generates synthetic telemetry and records its
decisions as JSON Lines. An ASP.NET Core API validates and archives those runs
in SQLite. The React dashboard replays the records and provides an
authenticated workspace for accounts and aircraft.

This manual covers installation, replay selection, dashboard interpretation,
accounts, roles, aircraft assignments, the native simulator, report export,
and common problems.

> AeroGuard is independent educational software. It isn't certified,
> connected to aircraft equipment, or intended for operational use.

## 2. Start the packaged demonstration

### Prerequisites

Install Docker Desktop or Docker Engine with Docker Compose v2. Clone the
[AeroGuard repository](https://github.com/dzikidziklasrwie/AeroGuard), then
open a terminal in its root directory.

### Configure the local administrator

Copy the supplied template:

```bash
cp .env.example .env
```

PowerShell:

```powershell
Copy-Item .env.example .env
```

Open `.env` and replace the example password. The file is ignored by Git. The
default username is `admin`; the plain password is supplied to the first API
start and is not written to source control.

### Build and start

```bash
docker compose up --build
```

The first build downloads base images and dependencies. It then:

1. compiles the C++ simulator;
2. generates all seven seed-42 replay artifacts;
3. publishes the .NET API;
4. builds the React application;
5. starts SQLite, the API, and the nginx dashboard proxy.

Wait for both services to become healthy, then open:

- Dashboard: [http://localhost:8080](http://localhost:8080)
- Direct API health: [http://localhost:8081/api/health](http://localhost:8081/api/health)
- OpenAPI: [http://localhost:8081/openapi/v1.json](http://localhost:8081/openapi/v1.json)

Stop the foreground process with `Ctrl+C`, then remove its containers and
network:

```bash
docker compose down
```

The `aeroguard-data` volume keeps the local SQLite database. Run `docker
compose down --volumes` only when you intentionally want to remove all local
accounts, aircraft, assignments, sessions, audit records, and imported runs.

## 3. Dashboard orientation

![AeroGuard dashboard replaying recorded simulator data](assets/dashboard-preview.png)

| Area | Purpose |
|---|---|
| `Scenario file` | Choose the newest recorded run for one of the seven scenarios. |
| `Overall recorded state` | Show the exact C++ state at the current replay position. |
| `Subsystem telemetry` | Show the latest visible record for each of six subsystems. |
| `Telemetry trend` | Plot one numeric measurement over simulation time. |
| `Recorded timeline` | List visible state transitions and diagnostic events. |
| `Simulation statistics` | Show values from the final C++ summary record. |
| `Recorded runs` | Select a specific archived or bundled replay. |
| `Operations workspace` | Sign in to manage fleet and, for administrators, accounts, roles, assignments, and audit. |
| `Architecture overview` | Explain C++, JSONL, REST, SQLite, and React boundaries. |
| `Download report` | Export the selected replay as a Markdown report. |

`Artifact loaded` means the dashboard accepted the manifest and selected JSONL
file. The data-source card identifies either the SQLite REST archive or the
bundled C++ files.

## 4. Recorded runs and automatic replay

The selected replay starts automatically. At its recorded end time it returns
to the first record and continues in a loop. The dashboard does not provide
user-operated replay actions.

To change the replay:

1. Find `Recorded runs`.
2. Review source, final state, duration, tick interval, and record count.
3. Select another run.
4. The dashboard resets to virtual time zero and starts the chosen run.

The `Scenario file` selector changes to the newest run for a scenario. The
`Recorded runs` selector can choose a specific entry from the archive. A
selection never changes source records or reruns the simulator.

If the API is unavailable, the same section lists the seven bundled JSONL
files. The dashboard labels that source clearly and applies the same parser.

## 5. Read health information

### Overall states

| State | Meaning in this simulator |
|---|---|
| `INITIALIZING` | The monitor lacks a complete first assessment. |
| `NORMAL` | All required observations are present and healthy. |
| `DEGRADED` | At least one valid reading is outside its nominal range, but no fault rule is active. |
| `FAULT` | A fault, invalid reading, or required heartbeat loss is active. |
| `RECOVERY` | The problem cleared, but the stability hold has not completed. |

`RECOVERY` is distinct from `NORMAL`. A healthy sample does not immediately
erase the recorded fault path.

### Subsystem cards

The six cards represent Power Supply, Cooling System, Temperature Sensors,
Communication Module, Onboard Computer, and Equipment Monitor.

Each card shows the latest visible health, sequence, timestamp, selected
measurements, and a short history line. A system-level fault does not mark an
unrelated subsystem as faulty. This makes isolation visible. An invalid record
remains validation evidence and is never converted into healthy telemetry by
the browser.

### Chart and timeline

Choose a subsystem above `Telemetry trend`, then choose a numeric metric. The
chart begins once at least two samples are visible.

The timeline contains accepted state transitions, diagnostic events, and
heartbeat loss or restoration. Severity and subsystem filters work together.
The newest 40 matching records at the current automatic replay position are
shown.

### Statistics

Statistics come from the final JSONL summary, not from browser estimates. They
include ticks, telemetry packets, invalid packets, missing heartbeats, dropped
messages, and accepted state transitions.

## 6. Supplied scenarios

| Scenario | What to observe | Typical supplied path |
|---|---|---|
| Normal operation | All six sources report within accepted limits. | `INITIALIZING -> NORMAL` |
| Communication interruption | Communications stops while other sources continue. | `INITIALIZING -> NORMAL -> FAULT -> RECOVERY -> NORMAL` |
| Invalid sensor data | Temperature samples fail validation. | `INITIALIZING -> NORMAL -> FAULT -> RECOVERY -> NORMAL` |
| Simulated overheating | Thermal telemetry crosses a fault threshold and later cools. | `INITIALIZING -> NORMAL -> FAULT -> RECOVERY -> NORMAL` |
| Power fluctuations | Power quality crosses a non-critical threshold and stabilizes. | `INITIALIZING -> NORMAL -> DEGRADED -> RECOVERY -> NORMAL` |
| Multiple subsystem failures | Overlapping faults show aggregation and isolation. | `INITIALIZING -> NORMAL -> FAULT -> RECOVERY -> NORMAL` |
| Recovery after temporary failure | A bounded communications failure shows the recovery hold. | `INITIALIZING -> NORMAL -> FAULT -> RECOVERY -> NORMAL` |

The checked-in seed-42 demonstrations finish in `NORMAL`; their timelines keep
the intermediate state history. Changing seed or timing can change values,
while an identical configuration remains deterministic.

## 7. Sign in and session behaviour

Public replay needs no account. Use `Operations workspace` for stored fleet
data.

1. Enter the configured username and password.
2. Select `Sign in`.
3. Confirm the session bar shows the expected account and roles.
4. Select `Sign out` when finished.

The browser keeps the bearer token in memory only. Refreshing or closing the
page removes it. The API stores only a SHA-256 value for each token. Logout,
password reset, and account deactivation revoke affected sessions.

Login is rate-limited. Failure always reports the same username-or-password
message so it doesn't reveal whether an account exists.

## 8. Roles and aircraft access

Roles apply across the application:

| Role | Permissions |
|---|---|
| `Admin` | Manage users, roles, aircraft, assignments, and audit records. |
| `Operator` | Create aircraft; update aircraft where the account is `OWNER` or `MAINTAINER`. |
| `Viewer` | Read aircraft assigned to the account. |

Aircraft assignment levels apply to one aircraft:

| Level | Permissions |
|---|---|
| `OWNER` | Read and update the aircraft. At least one owner must remain. |
| `MAINTAINER` | Read and update the aircraft. |
| `VIEWER` | Read the aircraft. |

An administrator sees all aircraft even without an assignment. Other users see
only assigned records.

## 9. Manage aircraft

### Create

An `Admin` or `Operator` can select `Add aircraft`, then provide:

- tail number: 2 to 16 ASCII letters, digits, or hyphens;
- display name;
- model;
- `ACTIVE`, `MAINTENANCE`, or `GROUNDED` status;
- optional notes up to 1000 characters.

The creator becomes the first `OWNER`. Tail numbers are case-insensitive and
unique.

### Update

Select an aircraft, edit its fields, and save. The browser submits the visible
revision as `expected_version`. If someone changed the record first, the API
returns a conflict. Refresh the record, review the newer values, and retry.

### Assign accounts

Administrators select an active account and an access level, then save the
assignment. Changing or removing the final `OWNER` is rejected until another
owner is assigned.

### Delete

Only an administrator can delete aircraft. Deletion also removes its
assignments. The audit record retains the actor and deleted aircraft identity.

## 10. Manage users and roles

Administrators can open `People and roles`.

### Create a user

Enter a username, display name, initial password, and at least one role.
Usernames use 3 to 64 ASCII letters, digits, dots, underscores, or hyphens and
start with a letter or digit. Normal passwords use 12 to 128 characters and at
least three character groups.

### Change roles or status

Select role checkboxes and save. The application prevents removal of the final
active `Admin` role. Deactivating an account revokes its sessions. The current
administrator cannot deactivate the account being used.

### Reset a password

Enter a new compliant password on the user card. All current sessions for that
account are revoked.

### Custom roles

Create a role with a name and optional description. Custom roles are labels
until application policy assigns them meaning. Delete is available only when
no user has the role. Built-in roles cannot be deleted.

## 11. Read the audit log

Administrators can open `Audit log` to view the latest 50 changes. Each row has
a timestamp, actor, action, and entity. Recorded actions include login, logout,
bootstrap, user creation, role and status changes, password reset, role
creation or deletion, aircraft changes, and assignment changes.

The log supports review of this local demonstration. It is not a regulated or
tamper-evident audit system.

## 12. Export a replay report

Select `Download report` after an artifact loads. The browser downloads:

```text
aeroguard-<scenario>-report.md
```

The report contains metadata, source artifact, configuration, final
statistics, state transitions, diagnostic events, and import warnings. If no
file appears, allow downloads for `localhost` and try again.

## 13. Run the native simulator

Follow [Build, install, and run](build-and-run.md), then list scenarios:

```bash
./build/debug/aeroguard --list-scenarios
```

Windows Developer PowerShell:

```powershell
.\build\debug\aeroguard.exe --list-scenarios
```

Run one scenario:

```bash
./build/debug/aeroguard \
  --scenario simulated-overheating \
  --seed 42 \
  --duration 9000 \
  --tick 100 \
  --output demo-output/overheating.jsonl
```

| Option | Meaning | Default |
|---|---|---|
| `--scenario <file-or-slug>` | Supplied scenario slug or JSON file path | `normal-operation` |
| `--seed <u32>` | Deterministic 32-bit random seed | `42` |
| `--duration <ms>` | Override scenario duration | Scenario value |
| `--tick <ms>` | Virtual simulation step | `100` |
| `--output <file>` | Destination JSONL path | `output/aeroguard.jsonl` |
| `--scenarios-dir <directory>` | Directory used for slug lookup | `scenarios` |
| `--list-scenarios` | Print the available catalog | Not applicable |
| `--export-all <directory>` | Run every supplied scenario and write a manifest | Not applicable |
| `--help`, `-h` | Print command help | Not applicable |

Export dashboard data:

```bash
./build/debug/aeroguard \
  --export-all frontend/public/data \
  --scenarios-dir scenarios \
  --seed 42
```

Reproduce an output by keeping scenario definition, seed, duration, and tick
interval unchanged. Automatic browser looping never affects simulator data.

## 14. Troubleshooting

| Symptom | Action |
|---|---|
| Port 8080 doesn't open | Run `docker compose ps`; check whether another process uses the port. |
| API is unhealthy | Run `docker compose logs api`; confirm `.env` exists and the data volume is writable. |
| Login fails on a fresh database | Set a non-placeholder `AEROGUARD_ADMIN_PASSWORD` before the first start. |
| Login fails on an existing database | Use the password that created the account; changing `.env` doesn't reset an existing user. |
| Dashboard labels data as bundled | The API isn't reachable. Confirm `/api/health` succeeds through port 8080. |
| Scenario manifest cannot load | Generate `frontend/public/data/manifest.json`; use Vite or nginx, not `file://`. |
| JSONL artifact cannot load | Confirm the referenced file exists and uses schema version 1; regenerate edited data. |
| Chart waits for samples | Let the automatic replay advance until two numeric samples are visible. |
| Timeline is empty | Reset severity and subsystem filters and wait for the relevant replay interval. |
| Aircraft update reports a conflict | Another change incremented the version; reload and review before retrying. |
| Final Admin or owner change is rejected | Assign another active administrator or aircraft owner first. |
| Report doesn't download | Allow downloads for the local site and confirm `Artifact loaded`. |
| CLI reports an unknown scenario | Run `--list-scenarios` and check `--scenarios-dir`. |

## 15. Suggested portfolio demonstration

1. Show `Recorded runs` and select Communication interruption.
2. Explain that selection resets the virtual clock and loops automatically.
3. Point out the global `FAULT` while unaffected subsystem cards continue.
4. Filter the timeline to Communication Module and show heartbeat evidence.
5. Show `RECOVERY` before the state returns to `NORMAL`.
6. Sign in and create a Viewer account and an aircraft.
7. Assign that Viewer, then explain the difference between application roles
   and per-aircraft access.
8. Show the audit entry and one stale-version protection test in the API suite.
9. Finish with the architecture view: C++ decides, JSONL records, .NET archives,
   SQLite stores, and React presents.

Don't claim aircraft validation, hard real-time behaviour, certification,
production deployment, or a hosted CI pass that hasn't been observed.

## 16. Verification references

[TASKS.md](../TASKS.md) records measured build and test results. The
[traceability matrix](traceability.md) maps each requirement to evidence. The
[API guide](../backend/README.md) documents routes and configuration.
