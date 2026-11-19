# AeroGuard delivery status

This file separates worktree artifacts from observed results. A configured CI
job isn't listed as passing until a hosted runner has executed it.

Snapshot date: 2026-11-19

## Workstream status

| Workstream | Artifact status | Observed verification |
|---|---|---|
| C++20 core, simulation, seven scenarios, and CLI | Complete in worktree | Clean MSVC and GCC builds with warnings as errors |
| GoogleTest and CLI suite | 36 discovered GoogleTests plus 8 CLI CTests | 44/44 passed on MSVC, GCC, and GCC with ASan/UBSan |
| React replay dashboard | Complete, responsive production build | ESLint, typecheck, 25/25 Vitest tests in 7 files, and Vite build passed |
| JSONL/manifest producer-consumer contract | C++ export, seven authentic replays, parser, and manifest | All seven generated files loaded without parser warnings |
| ASP.NET Core API, SQLite, accounts, and fleet access | Minimal API, archive importer, bearer sessions, roles, aircraft assignments, and audit records | Release build, format check, and 31/31 xUnit integration tests passed |
| Documentation and CV material | User manual, requirements, design, build, tests, trade-offs, interview notes, and measured CV copy | Cross-checked against the current code and results |
| Windows/Linux GitHub Actions | Workflow defines MSVC, GCC, sanitizers, clang-tidy, frontend, backend, and Docker jobs | No hosted run observed yet |
| Multi-stage Docker demo | Simulator data generation, .NET API, React build, nginx, and SQLite volume | Non-root API image and integrated stack built; archive, login, and HTTP smoke checks passed |
| Portfolio visuals | Generated hero plus real dashboard capture and provenance | Production dashboard captured at 1600 x 1200 |

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
- [x] Authentic C++ replay artifacts and producer-consumer contract test.
- [x] Functional requirements `REQ-001` through `REQ-042` and traceability.
- [x] Architecture, state, sequence, deployment, and interview diagrams.
- [x] Windows, Linux, frontend, Docker, install, and troubleshooting guides.
- [x] End-user manual for dashboard operation, CLI use, report export, scenario
  interpretation, and portfolio demonstration.
- [x] Design decisions, limitations, roadmap, educational-scope disclaimer,
  CV wording, visual assets, and asset provenance.
- [x] CI jobs for MSVC, GCC, frontend, backend, sanitizers, clang-tidy, Docker
  smoke testing, and artifact upload.

## Verification checklist

- [x] Clean MSVC Debug configure/build with `AEROGUARD_WARNINGS_AS_ERRORS=ON`.
- [x] MSVC CTest: 44/44 passed.
- [x] Ubuntu 24.04 / GCC 13.3 container build with `-Werror`.
- [x] GCC CTest: 44/44 passed.
- [x] GCC ASan/UBSan CTest: 44/44 passed.
- [x] Clang 18.1 clang-tidy analysis build.
- [x] Repeat a seeded run and compare byte-identical JSONL output.
- [x] Export all seven replays and validate the manifest.
- [x] `npm ci`.
- [x] `npm run lint`.
- [x] `npm run typecheck`.
- [x] `npm test -- --run`: 25/25 passed in 7 files.
- [x] `npm run build`.
- [x] `npm audit --audit-level=moderate`: 0 vulnerabilities.
- [x] Load all seven C++-generated replays through the frontend parser.
- [x] Release-build the API and run 31/31 xUnit integration tests.
- [x] `dotnet format --verify-no-changes`: no changes reported.
- [x] Build the multi-stage API/dashboard images and run archive, login, and
  HTTP smoke checks.
- [ ] Windows MSVC GitHub Actions job observed passing.
- [ ] Linux GCC GitHub Actions job observed passing.
- [ ] Frontend, backend, sanitizer, analysis, and Docker GitHub Actions jobs
  observed passing.
- [ ] Fresh-clone README walkthrough of the published revision.

## Evidence log

| Environment | Observed evidence |
|---|---|
| Windows native | MSVC 19.51.36248, strict `/W4 /WX`, clean Debug build, then `ctest --preset debug --output-on-failure`; 44/44 passed. |
| Linux native container | Ubuntu 24.04, GCC 13.3.0, CMake 3.28.3, `-Wall -Wextra -Wpedantic -Wconversion -Wshadow -Werror`; the build completed and 44/44 tests passed. |
| Dynamic analysis container | The same GCC build with AddressSanitizer and UndefinedBehaviorSanitizer; 44/44 tests passed with leak detection and halt-on-UB settings enabled. |
| Static analysis container | Ubuntu 24.04, Clang/clang-tidy 18.1.3; the analysis-enabled build completed. |
| Frontend | Node 24.13.1 and npm 11.8.0; the locked install, ESLint, TypeScript, 25/25 Vitest tests in 7 files, and Vite production build passed. `npm audit` reported 0 vulnerabilities. |
| Backend | .NET 10 Release build and 31/31 xUnit integration tests passed; `dotnet format --verify-no-changes` reported no changes. |
| Docker | The non-root API image built, imported all seven replays, reported ready, and accepted the configured administrator login. Compose started the API, SQLite volume, nginx proxy, and dashboard; the root page and API proxy smoke checks passed. |
| Visual review | Headless Chrome captured the production container at 1600 x 1200 after replay data loaded. |

This snapshot has no observed GitHub-hosted workflow result. We don't claim a
coverage percentage.
