# Requirements-to-tests traceability

## Verification snapshot

These statuses come from commands run on 2026-11-19:

- MSVC 19.51 Debug with `/WX`: 44/44 CTest tests passed.
- Ubuntu 24.04 and GCC 13.3 with `-Werror`: 44/44 CTest tests passed.
- GCC 13.3 with ASan/UBSan: 44/44 CTest tests passed.
- Clang/clang-tidy 18.1 completed the analysis build.
- The locked frontend install passed lint, typecheck, 25/25 Vitest tests in 7
  files, the production build, and the dependency audit; the audit reported 0
  vulnerabilities.
- The .NET Release build and 31/31 xUnit integration tests passed; `dotnet
  format --verify-no-changes` reported no changes.
- The non-root Docker API image built, imported seven runs, reported ready, and
  accepted the configured administrator login.
- GitHub Actions run
  [`44619482612`](https://github.com/dzikidziklasrwie/AeroGuard/actions/runs/44619482612)
  passed the MSVC, GCC, sanitizer, clang-tidy, frontend, API, and Docker jobs.

`Verified locally` records direct command evidence. `Verified in hosted CI`
means the published feature-branch run completed successfully.

| Requirement | Automated or review evidence | Level | Status |
|---|---|---|---|
| REQ-001 | `REQ001_DeterministicReplay.SameSeedProducesByteIdenticalJsonLines`; `.RawMt19937MappingHasPortableGoldenSample` | C++ integration | Verified on MSVC and GCC |
| REQ-002 | `REQ002_ScenarioConfiguration.ResolvesBothSlugAndExplicitFile`; `REQ002_CLIListScenarios`; unknown-scenario CLI check | C++/CLI | Verified on MSVC and GCC |
| REQ-003 | `REQ003_SimulationConfiguration.*`; invalid tick, seed, and duration CLI checks | C++ unit/CLI | Verified on MSVC and GCC |
| REQ-004 | `REQ004_Simulator.NormalRunPollsAllSixSubsystemsOnEveryTick` | C++ integration | Verified on MSVC and GCC |
| REQ-005 | `REQ005_Telemetry.AcceptsHealthyTypedPayloadForAllSixSubsystems`; `REQ005_JSONLinesTelemetry.*` | C++ unit/integration | Verified on MSVC and GCC |
| REQ-006 | Five `REQ006_TelemetryValidation` cases, two monitor-validation cases, and invalid-sensor integration case | C++ unit/integration | Verified on MSVC and GCC |
| REQ-007 | `REQ007_HeartbeatMonitoring.*`; `REQ007_Integration.CommunicationOutageFaultsThenRecoversToNormal` | C++ unit/integration | Verified on MSVC and GCC |
| REQ-008 | Three `REQ008_BoundedQueue` cases covering capacity/FIFO, threaded transfer, and close | C++ unit | Verified on MSVC and GCC |
| REQ-009 | `REQ009_VirtualClock.AdvancesAndResetsWithoutWallClockDelay`; deterministic replay cases | C++ unit/integration | Verified on MSVC and GCC |
| REQ-010 | `REQ010_SimulatedHAL.IsolatesInjectedFaultToConfiguredSubsystemAndWindow` | C++ integration | Verified on MSVC and GCC |
| REQ-011 | `REQ011_StateMachine.SupportsAllFiveStatesThroughValidRecoveryPaths`; `REQ011_HealthMonitor.*` | C++ unit | Verified on MSVC and GCC |
| REQ-012 | Three `REQ012_StateMachine` cases, including the complete 5 x 5 matrix | C++ unit | Verified on MSVC and GCC |
| REQ-013 | `REQ013_SubsystemIsolation.FaultDoesNotOverwriteHealthySubsystemStatus` | C++ unit | Verified on MSVC and GCC |
| REQ-014 | `REQ014_FaultHandling.InvalidTelemetryMovesOverallSystemToFault`; JSON transition checks | C++ unit/integration | Verified on MSVC and GCC |
| REQ-015 | `REQ015_Recovery.EnforcesRecoveryHoldBeforeReturningToNormal`; outage integration case | C++ unit/integration | Verified on MSVC and GCC |
| REQ-016 | `REQ016_EventLogging.EmitsStructuredHeartbeatAndTransitionEvents`; JSONL checks | C++ unit/integration | Verified on MSVC and GCC |
| REQ-017 | `REQ017_ScenarioCatalog.ContainsAllSevenRequiredScenarioFiles`; CLI listing | C++ unit/CLI | Verified on MSVC and GCC |
| REQ-018 | `REQ018_JSONLines.WritesMetadataFirstAndSummaryLastWithSchemaV1` | C++ integration | Verified on MSVC and GCC |
| REQ-019 | JSONL integration check plus Vitest parser coverage for every schema-v1 record type and C++-exported file | Producer/consumer contract | Verified locally |
| REQ-020 | `REQ020_CLIExportAll` validates seven files and manifest shape; exported-data Vitest loads all seven | CLI/frontend integration | Verified locally |
| REQ-021 | Two `REQ021_ScenarioDiagnostics` cases and six CLI success/error checks | C++/CLI | Verified on MSVC and GCC |
| REQ-022 | Parser Vitest cases, C++ artifact contract test, and architecture ownership review | Frontend/contract/review | Verified locally |
| REQ-023 | App component test asserts overall state and all six subsystem cards; exported-data test asserts six sources | Frontend component/contract | Verified locally |
| REQ-024 | App component test renders telemetry-trend and recorded-timeline sections; production-browser capture | Frontend component/review | Verified locally |
| REQ-025 | App interaction test filters the timeline by severity and subsystem | Frontend component | Verified locally |
| REQ-026 | `usePlayback` fake-timer test covers automatic start, end-to-start looping, and reset on dataset change; App test covers run selection | Frontend hook/component | Verified locally |
| REQ-027 | App test checks statistics, architecture, and enabled report control; `buildSimulationReport` validates report content | Frontend component/unit | Verified locally |
| REQ-028 | Malformed/unknown/missing-metadata parser cases; App manifest-error and retry test | Frontend unit/component | Verified locally |
| REQ-029 | Local MSVC and containerized GCC strict builds and 44-test runs; CI matrix definition | Portability/CI | Verified locally and in hosted CI |
| REQ-030 | Local native, frontend, and .NET checks, 44-test ASan/UBSan run, Clang 18 clang-tidy build, and corresponding CI jobs | Analysis/CI | Verified locally and in hosted CI |
| REQ-031 | Multi-stage Docker builds, seven generated replays, non-root API health/login check, Compose configuration, and dashboard/API proxy smoke test | Docker | Verified locally and in hosted CI |
| REQ-032 | README and user-manual disclaimers, dashboard boundary notice, and production-browser review | Documentation/UI review | Verified locally |
| REQ-033 | `ArtifactImporterTests` cover first import, idempotent repeat, tamper rejection, and rollback; API factory imports a new temporary database | .NET integration | Verified locally |
| REQ-034 | `ApiEndpointTests` cover scenarios, run queries, replay bytes, ordered/filtered records, validation, OpenAPI, and missing resources | .NET HTTP integration | Verified locally |
| REQ-035 | `AeroGuardApiFactory` supplies a test-only runtime password; bootstrap and repository review confirm no plain credential is stored | .NET integration/review | Verified locally |
| REQ-036 | Access tests cover login, profile, logout, reset, deactivation, generic failure, and rejected revoked sessions | .NET HTTP integration | Verified locally |
| REQ-037 | Access tests cover bearer challenges, duplicate users, retained Admin role, final-Admin removal, self-deactivation, and system-role deletion | .NET HTTP integration | Verified locally |
| REQ-038 | Viewer tests cover assigned list access and hidden unassigned detail | .NET HTTP integration | Verified locally |
| REQ-039 | Operator tests cover create, normalized tail number, authorized update, incremented version, and stale-update conflict | .NET HTTP integration | Verified locally |
| REQ-040 | Assignment tests cover Viewer access and final-owner removal rejection; SQLite triggers protect concurrent paths | .NET HTTP integration/storage review | Verified locally |
| REQ-041 | `AuditLogRecordsAuthenticatedChanges` checks login and aircraft-create audit entries | .NET HTTP integration | Verified locally |
| REQ-042 | API-client tests cover scenario/run REST selection and static fallback; workspace review confirms no browser persistence API | Frontend unit/component/review | Verified locally |

[TASKS.md](../TASKS.md) records the detailed commands, versions, and hosted
run link.
