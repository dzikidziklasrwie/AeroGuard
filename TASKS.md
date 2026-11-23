# AeroGuard delivery status

This file separates worktree artifacts from observed results. A configured CI
job isn't listed as passing until a hosted runner has executed it.

Snapshot date: 2026-11-21

## Workstream status

| Workstream | Artifact status | Observed verification |
|---|---|---|
| C++20 core, simulation, seven scenarios, and CLI | Complete in worktree | Clean MSVC and GCC builds with warnings as errors |
| GoogleTest and CLI suite | 37 discovered GoogleTests plus 8 CLI CTests | Current local suite passed 45/45; previous revision passed 44/44 on MSVC, GCC, and GCC with ASan/UBSan |
| React replay and operations dashboard | Recorded replay, packaged world map, route phase/progress, zoom and pan, selected-aircraft identity, flight chat, permission-aware controls, scenario-linked aircraft effects, SignalR client, and REST fallback | ESLint, typecheck, 47/47 Vitest tests in 10 files, Vite build, and npm audit passed |
| JSONL/manifest producer-consumer contract | C++ export, seven authentic replays, parser, and manifest | All seven generated files loaded without parser warnings |
| ASP.NET Core API, SQLite, accounts, fleet, and simulated operations | Minimal API, archive importer, bearer sessions, administrator-controlled assignments, destination routing, operation permissions, active flights, chat, session-aware SignalR delivery, and audit records | Current xUnit suite passed 67/67 |
| Documentation and CV material | User manual, requirements, design, build, tests, trade-offs, interview notes, and measured CV copy | Cross-checked against the current code and results |
| Windows/Linux GitHub Actions | Workflow defines MSVC, GCC, sanitizers, clang-tidy, frontend, backend, and Docker jobs with Node 24 action runtimes and explicit timeouts | Run `44854113152` passed all seven jobs on implementation commit `47d38c5` |
| Multi-stage Docker demo | Independent simulator, .NET API, React, nginx, SignalR proxy, and SQLite stages | API/dashboard cached rebuild measured 3.5 seconds locally; both services became healthy and exposed all seven replays |
| Portfolio visuals | Generated hero plus real dashboard and operations-map captures with provenance | Production dashboard captured at 1600 x 1200; authenticated map workspace captured at 1446 x 904 |

## Implementation checklist

- [x] Six typed fictional subsystem models and simulated HAL boundary.
- [x] Telemetry validation, heartbeat monitoring, isolation, and recovery hold.
- [x] Complete five-state direct-transition allow-list and rejection policy.
- [x] Seven deterministic scenario files and seed/configuration controls.
- [x] Bounded synchronized queue with explicit full, empty, and close behaviour.
- [x] JSONL metadata, telemetry, event, transition, and summary records.
- [x] Single-scenario CLI, diagnostics, scenario listing, and export-all manifest.
- [x] GoogleTest unit/integration cases and CLI-level CTest checks.
- [x] Dashboard parser, automatic replay, run archive, charts, cards, filters,
  timeline, statistics, error states, and Markdown report export.
- [x] REST archive, SQLite import, bearer sessions, role-based accounts,
  aircraft assignments, optimistic updates, and audit records.
- [x] Authenticated Natural Earth world map, active simulated flights, stored
  C++ telemetry snapshots, flight chat, REST/SignalR client updates, and stale
  event rejection.
- [x] Admin bypass, `OWNER` operation checks, per-account overrides, role
  defaults, session-aware SignalR recipients, and fixed-window write limits.
- [x] Administrator aircraft assignment with People-to-Fleet role matching:
  Operator, Maintainer, or Viewer labels map to explicit API access levels.
- [x] ISO country routes, mapped-aircraft `ACTIVE` invariant, atomic chat
  authorization, and 500-row per-flight chat retention.
- [x] Per-flight broadcast failure isolation and stale live-event rejection.
- [x] Event sequences reserved before asynchronous snapshot work, preventing a
  delayed periodic update from reviving a removed or reconfigured flight.
- [x] Coordinate validation, bounded route motion, sphere-clipped aircraft
  layer, and inward-facing edge labels.
- [x] Always-visible tail-number markers, linked map shadows, selected-aircraft
  card, fading contrails, and scenario-specific fault animations.
- [x] Sustained overheating, 20-second fire, airframe breakup, deterministic
  debris descent, land impact and wreckage, or water splash and floating debris.
- [x] A selected-aircraft scenario control that preserves the flight's
  destination and makes the target tail number explicit.
- [x] Continent-as-destination routing, great-circle `TRANSIT` capped at 30
  seconds, bounded `PATROL`, route progress, and local country patrols.
- [x] Display bounds of latitude -82 to 82 and longitude -176 to 176, plus
  last-valid-position retention for malformed live updates.
- [x] Pointer-centered 1x-6x zoom, pan, reset, selected-aircraft focus,
  zoom-aware placement, and drag suppression.
- [x] Idle broadcaster exit, batched active-recipient checks, one prepared
  flight snapshot per update, single-pass replay state, and lazy map loading.
- [x] Loopback-only direct Compose API port and one-hop nginx forwarded headers.
- [x] Independent cached Docker build paths: routine API and dashboard builds
  reuse verified replay artifacts; the simulator remains an explicit target.
- [x] Authentic C++ replay artifacts and producer-consumer contract test.
- [x] Seven regenerated 40-to-105-second replay artifacts and duration checks.
- [x] Functional requirements `REQ-001` through `REQ-058` and traceability.
- [x] Architecture, state, sequence, deployment, and interview diagrams.
- [x] Windows, Linux, frontend, Docker, install, and troubleshooting guides.
- [x] End-user manual for dashboard operation, CLI use, report export, scenario
  interpretation, and portfolio demonstration.
- [x] Design decisions, limitations, roadmap, educational-scope disclaimer,
  CV wording, visual assets, and asset provenance.
- [x] CI jobs for MSVC, GCC, frontend, backend, sanitizers, clang-tidy, Docker
  smoke testing, and artifact upload.
- [x] Node 24 action runtimes, per-job timeouts, read-only checkout credentials,
  and a generated CI smoke-test password.

## Verification checklist

- [x] Clean MSVC Debug configure/build with `AEROGUARD_WARNINGS_AS_ERRORS=ON`.
- [x] Current local CTest: 45/45 passed.
- [x] Previous-revision MSVC CTest: 44/44 passed.
- [x] Ubuntu 24.04 / GCC 13.3 container build with `-Werror`.
- [x] Previous-revision GCC CTest: 44/44 passed.
- [x] Previous-revision GCC ASan/UBSan CTest: 44/44 passed.
- [x] Clang 18.1 clang-tidy analysis build.
- [x] Repeat a seeded run and compare byte-identical JSONL output.
- [x] Export all seven replays and validate the manifest.
- [x] `npm ci`.
- [x] `npm run lint`.
- [x] `npm run typecheck`.
- [x] `npm test -- --run`: 47/47 passed in 10 files.
- [x] `npm run build`.
- [x] `npm audit --audit-level=moderate`: 0 vulnerabilities.
- [x] Load all seven C++-generated replays through the frontend parser.
- [x] Run 67/67 xUnit test cases for the current API revision.
- [x] Sample every continent patrol profile across 24 hours and verify a route
  remains finite after 50 years of elapsed time.
- [x] `dotnet format --verify-no-changes`: no changes reported.
- [x] Build the multi-stage API/dashboard images and run archive, login, and
  HTTP smoke checks.
- [x] Windows MSVC GitHub Actions job observed passing.
- [x] Linux GCC GitHub Actions job observed passing.
- [x] Frontend, backend, sanitizer, analysis, and Docker GitHub Actions jobs
  observed passing.
- [x] Run the preceding map-containment revision in hosted CI.
- [x] Run the current destination-routing, viewport, scenario, and performance
  revision in hosted CI.
- [x] Verify a real browser SignalR client, nginx WebSocket upgrade, and Docker
  network path as one stack.
- [ ] Fresh-clone README walkthrough of the published revision.

## Evidence log

| Environment | Observed evidence |
|---|---|
| Previous-revision Windows native | MSVC 19.51.36248, strict `/W4 /WX`, clean Debug build, then `ctest --preset debug --output-on-failure`; 44/44 passed. |
| Previous-revision Linux native container | Ubuntu 24.04, GCC 13.3.0, CMake 3.28.3, `-Wall -Wextra -Wpedantic -Wconversion -Wshadow -Werror`; the build completed and 44/44 tests passed. |
| Previous-revision dynamic analysis container | The same GCC build with AddressSanitizer and UndefinedBehaviorSanitizer; 44/44 tests passed with leak detection and halt-on-UB settings enabled. |
| Previous-revision static analysis container | Ubuntu 24.04, Clang/clang-tidy 18.1.3; the analysis-enabled build completed. |
| Current native | The current local suite passed 45/45, including the seven-scenario duration rule. |
| Frontend | Node 24.13.1 and npm 11.8.0; the locked install, ESLint, TypeScript, 47/47 Vitest tests in 10 files, and Vite production build passed. `npm audit` reported 0 vulnerabilities. |
| Backend | The current .NET suite passed 67/67 xUnit test cases, including 24-hour continent patrol samples, a 50-year finite-route case, route-transition continuity, and overheating motion hold. |
| Previous Docker/browser | API and dashboard images from the preceding revision reported healthy. An authenticated headless Chrome session rendered three flight markers, completed one `/hubs/operations` WebSocket handshake through nginx, and received four WebSocket frames. |
| Current GitHub Actions | [Run `44854113152`](https://github.com/dzikidziklasrwie/AeroGuard/actions/runs/44854113152) completed successfully with MSVC, GCC, ASan/UBSan, clang-tidy, frontend, API, and Docker jobs on implementation commit `47d38c5`. |
| Visual review | Headless Chrome captured the production dashboard at 1600 x 1200 and the authenticated operations-map workspace at 1446 x 904 after API data loaded. |

We don't claim a coverage percentage.
