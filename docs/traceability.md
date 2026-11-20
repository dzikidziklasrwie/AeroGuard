# Requirements-to-tests traceability

## Verification snapshot

The current feature checks run on 2026-11-20 reported:

- The native suite passed 45/45 CTest checks, including the seven-scenario
  40-to-55-second duration rule.
- The locked frontend install passed lint, typecheck, 37/37 Vitest tests in 9
  files, including stale live-event ordering and map containment; the
  production build and dependency audit also passed, and the audit reported 0
  vulnerabilities.
- The .NET suite passed 53/53 xUnit test cases, including administrator
  password synchronization, operations authorization, bounded route motion,
  country/polar validation, active-flight status, chat, hub token scope, and
  revoked-session recipient checks.

The feature branch supplied these platform and deployment checks:

- GitHub Actions run
  [`44812965465`](https://github.com/dzikidziklasrwie/AeroGuard/actions/runs/44812965465)
  passed the MSVC, GCC, ASan/UBSan, clang-tidy, frontend, API, and Docker jobs
  on commit `0488ea0`.
- The non-root Docker API image built, imported seven runs, reported ready, and
  accepted the configured administrator login.
- The workflow uses Node 24 action runtimes and sets a timeout for every job.

A local headless-Chrome session completed the nginx WebSocket handshake and
received four SignalR frames. The automated tests negotiate SignalR but don't
connect a real WebSocket client end to end.

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
| REQ-029 | Strict MSVC and GCC builds plus the current 45-test native suite in run `44812965465` | Portability/CI | Verified in hosted CI |
| REQ-030 | Native, frontend, API, ASan/UBSan, and clang-tidy jobs in run `44812965465` | Analysis/CI | Verified in hosted CI |
| REQ-031 | Multi-stage Docker build, service health checks, authenticated API smoke request, and SignalR negotiation in run `44812965465`; local browser WebSocket and three-marker capture | Docker/browser/review | Docker verified in hosted CI; full browser path verified locally |
| REQ-032 | README, user-manual, map boundary notice, and limitations review | Documentation/UI review | Verified locally |
| REQ-033 | `ArtifactImporterTests` cover first import, idempotent repeat, tamper rejection, and rollback; API factory imports a new temporary database | .NET integration | Verified locally |
| REQ-034 | `ApiEndpointTests` cover scenarios, run queries, replay bytes, ordered/filtered records, validation, OpenAPI, and missing resources | .NET HTTP integration | Verified locally |
| REQ-035 | Runtime-only bootstrap and password-sync tests confirm that Compose can update an existing administrator, revoke older sessions, and store no plain credential | .NET integration/review | Verified locally |
| REQ-036 | Access tests cover login, profile, logout, reset, deactivation, generic failure, and rejected revoked sessions | .NET HTTP integration | Verified locally |
| REQ-037 | Access tests cover bearer challenges, duplicate users, retained Admin role, final-Admin removal, self-deactivation, and system-role deletion | .NET HTTP integration | Verified locally |
| REQ-038 | Viewer tests cover assigned list access and hidden unassigned detail | .NET HTTP integration | Verified locally |
| REQ-039 | Operator tests cover create, normalized tail number, authorized update, incremented version, and stale-update conflict | .NET HTTP integration | Verified locally |
| REQ-040 | Assignment tests cover Viewer access and final-owner removal rejection; SQLite triggers protect concurrent paths | .NET HTTP integration/storage review | Verified locally |
| REQ-041 | `AuditLogRecordsAuthenticatedChanges` checks login and aircraft-create entries; operation-permission and flight audit calls have implementation review | .NET HTTP integration/review | Partly verified; dedicated operation-audit assertions pending |
| REQ-042 | API-client tests cover scenario/run REST selection and static fallback; workspace review confirms no browser persistence API | Frontend unit/component/review | Verified locally |
| REQ-043 | `OperationsMap` geometry, attribution, marker-selection, detail, and telemetry component case | Frontend component | Verified locally |
| REQ-044 | `ViewerSeesAssignedFlightButCannotPlaceOrReconfigureIt`; `RemovingAssignmentImmediatelyRevokesFlightVisibility`; `UnassignedAccountCannotReadAnotherFlightsChat` | .NET HTTP integration | Verified locally |
| REQ-045 | Operator, Viewer, override, and Admin flight-operation cases; rate-limit policy review | .NET HTTP integration/review | Partly verified; limit-exhaustion test pending |
| REQ-046 | `PermissionOverrideCanStopOwnerFromRemovingFlight`; `ExplicitEmptyPermissionOverrideRemovesOperatorDefaults`; Admin endpoint and audit review | .NET HTTP integration/review | Verified locally |
| REQ-047 | Placement, polar-coordinate, ISO country-code, invalid-route, stale-version, archive-run, active-flight status/deletion, and frontend request-shape checks | .NET/frontend integration | Verified locally |
| REQ-048 | `FlightMotionTests` cover elapsed motion, recorded-duration loop, route bounds, six-subsystem completeness, and fault-to-critical mapping | .NET unit | Verified in hosted CI |
| REQ-049 | Hub authentication/query-scope and revoked-session tests; SignalR client reconnect-resync and stale-event-ordering tests; local Chrome/nginx WebSocket handshake and frames; per-flight failure-isolation review | .NET/frontend/browser/review | Partly verified; automated real-client revocation, failure-injection, and limit-exhaustion tests pending |
| REQ-050 | Server-generated share, hidden-chat, owner-gating, 500-character checks, and atomic insert plus 500-row retention-trigger review; rate-limit policy review | .NET/frontend integration/storage review | Partly verified; atomic-race, limit-exhaustion, and retention-cap tests pending |
| REQ-051 | `REQ051_ScenarioDuration.KeepsEveryPackagedScenarioBetween40And55Seconds`; export, importer, and authentic-data duration checks | C++/CLI/.NET/frontend contract | Verified locally |
| REQ-052 | `OperationsMap` labelled session-demo and authentication handling cases; storage review | Frontend component/review | Verified locally |
| REQ-053 | `RouteStaysWithinPublishedCoordinateBounds`; `OperationsMap` boundary clipping, inward-label, out-of-range live-update, and REST reconfiguration cases | .NET unit/frontend component | Verified in hosted CI |

[TASKS.md](../TASKS.md) records the detailed commands, versions, and hosted
run link.
