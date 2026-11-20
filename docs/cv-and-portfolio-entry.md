# CV and portfolio entry

Use only the claims that match the revision you publish at the
[AeroGuard repository](https://github.com/dzikidziklasrwie/AeroGuard). Don't add
a coverage number, deployment claim, or passing CI badge until you have the
corresponding result.

## Two-line CV entry

AeroGuard - Avionics Health Monitoring Platform | C++20, ASP.NET Core, SQLite,
React, TypeScript<br>
Built a seeded virtual-time simulator for six fictional aircraft subsystems,
with typed telemetry, bounded queues, heartbeat/fault handling, versioned JSONL
diagnostics, a REST archive, role-based fleet management, and a React
world-map dashboard with assigned flights, chat, and SignalR updates.
[GitHub](https://github.com/dzikidziklasrwie/AeroGuard)

## Evidence-based bullet variants

Choose the statement that best fits the job description:

- Designed a C++20 simulation core with scoped domain types,
  `std::variant` telemetry, a fixed-capacity queue, injected clock/HAL
  boundaries, and an explicit five-state health policy.

- Added GoogleTest coverage for transition rules, telemetry
  boundaries, queue behaviour, heartbeat timeouts, configuration errors,
  seeded replay, isolation, recovery, and 40-to-55-second packaged scenarios.
  The current revision passed 45/45 native tests, 48/48 .NET tests, and 34/34
  Vitest checks in 9 files.

- Built an ASP.NET Core Minimal API that validates and imports C++ JSONL into
  SQLite, exposes run history through REST, and manages revocable bearer
  sessions, roles, aircraft assignments, per-account operation permissions,
  active simulated flights, chat, optimistic updates, and audit records.

- Connected the C++ producer to a TypeScript/React dashboard through REST with
  a packaged JSONL fallback, then added a Natural Earth operations map with
  authorized SignalR updates and built a multi-stage Docker stack with nginx,
  a non-root API image, and persistent SQLite storage.

These measured results are from local runs on 2026-11-20. The repository
contains a GitHub Actions workflow, but don't call hosted CI "passing" until it
has run on the published revision.

## Tech stack line

C++20, CMake Presets, Ninja, GoogleTest, nlohmann/json, ASP.NET Core .NET 10,
Minimal APIs, SQLite, xUnit, React 18, TypeScript, Vite, Vitest, React Testing
Library, SignalR, D3 Geo, Natural Earth world-atlas, GitHub Actions, Docker,
ASan/UBSan, clang-tidy

## Portfolio visuals

| Asset | Provenance |
|---|---|
| `docs/assets/aeroguard-hero.png` | Generated concept art. `docs/assets/README.md` records the exact prompt and tool. |
| `docs/assets/dashboard-preview.png` | Browser capture of the production container using C++-generated replay data. |

## GitHub description

Deterministic C++20 health-monitoring simulator with an ASP.NET Core REST
archive, SQLite role and fleet management, and a React/TypeScript dashboard.
The signed-in workspace adds assigned simulated flights, chat, and authorized
map updates. Educational and non-certified.

## 30-second interview pitch

AeroGuard is a C++20 desktop simulator for health monitoring across six
fictional aircraft subsystems. It uses a seeded virtual clock, typed telemetry,
a bounded queue, heartbeat detection, and an explicit five-state policy. C++
writes versioned JSONL. A .NET API archives it in SQLite and React presents it,
so neither layer duplicates health logic. The same API provides revocable
sessions, roles, aircraft assignments, account operation permissions, simulated
map flights, flight chat, version checks, and audit records.

## 90-second interview pitch

I built AeroGuard to practise embedded C++ design in a project I can run on a
desktop. Six simulated subsystems produce strongly typed telemetry. A manual
simulation clock and fixed random seed make each run repeatable, while a
fixed-capacity queue makes overload behaviour explicit. The health monitor
validates each payload, tracks heartbeat age, keeps subsystem state separate,
and asks a state machine to accept or reject transitions between initializing,
normal, degraded, fault, and recovery.

The C++ process is the decision authority. It records metadata, telemetry,
events, state changes, and a summary as schema-versioned JSON Lines. ASP.NET
Core validates and imports each artifact once by SHA-256, preserves the
original bytes, and exposes runs and filtered records through REST. React
automatically loops the chosen run and provides filtering, charts, cards,
statistics, report export, and an authenticated fleet workspace. It doesn't
calculate a second version of system health. The signed-in map selects stored
C++ telemetry, moves synthetic markers on a separate display clock, and sends
authorized changes through SignalR.

Virtual time proves logical repeatability, not real-time deadlines. The
synchronized bounded queue has predictable slot
storage, but it isn't a lock-free real-time primitive. File replay is easy to
inspect and test, but the map's SignalR updates still aren't live target
communication. The project uses
fictional data and makes no certification or proprietary-system claim.
