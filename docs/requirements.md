# Functional requirements

## Purpose and language

This document defines AeroGuard's testable behaviour. `Shall` marks a normative
requirement. [The traceability matrix](traceability.md) links implementation and
automated tests to each identifier. An identifier alone does not prove
verification.

## Simulation and telemetry

| ID | Requirement | Acceptance evidence |
|---|---|---|
| REQ-001 | Given the same scenario, seed, duration, and tick interval, the simulator shall produce the same ordered simulation records. | Test compares two independent runs without excluding fields. |
| REQ-002 | The CLI shall accept a scenario by built-in slug or scenario file and reject unknown scenarios with a non-zero exit code. | Valid and invalid CLI tests. |
| REQ-003 | The user shall be able to configure a 32-bit random seed, simulation duration, and tick interval. | CLI/configuration tests and metadata record. |
| REQ-004 | The simulator shall model power supply, cooling, temperature sensing, communications, onboard computing, and equipment-status monitoring. | Scenario integration test observes all six subsystem identifiers. |
| REQ-005 | Telemetry shall use typed domain fields and identify its source subsystem, sequence, timestamp, health, validity, reason, and measurements. | Compile-time model use plus JSON contract test. |
| REQ-006 | Out-of-range, non-finite, malformed, or semantically invalid telemetry shall be rejected or marked invalid without terminating the simulation. | Validation boundary and malformed-input tests. |
| REQ-007 | The monitor shall detect a missing heartbeat after its configured tolerance and emit diagnostic evidence. | Virtual-clock heartbeat test. |
| REQ-008 | Inter-component delivery shall use a bounded queue with explicit full and empty behaviour. | Capacity, FIFO, overflow, and empty-pop tests. |
| REQ-009 | The main simulation loop shall advance from a configurable simulation clock rather than sleep on wall-clock time. | Clock unit test and deterministic replay test. |
| REQ-010 | Scenario execution shall not require physical hardware; hardware-facing behaviour shall be accessed through a simulated abstraction boundary. | Integration test uses the simulated HAL. |

## Health and fault handling

| ID | Requirement | Acceptance evidence |
|---|---|---|
| REQ-011 | Overall health shall be represented by `INITIALIZING`, `NORMAL`, `DEGRADED`, `FAULT`, and `RECOVERY`. | State enum and state-machine tests. |
| REQ-012 | Only transitions listed in the state-machine specification shall be accepted; every other direct transition shall be rejected without changing state. | Complete transition-matrix parameterized test. |
| REQ-013 | A non-critical subsystem problem shall be isolatable so healthy subsystems continue producing telemetry. | Subsystem-isolation integration test. |
| REQ-014 | Critical or compounded failures shall move overall health to `FAULT` according to the state policy and emit a transition record. | Fault-scenario integration tests. |
| REQ-015 | A temporary fault shall pass through `RECOVERY` and a stability observation before returning to a healthier state. | Recovery scenario and transition-order test. |
| REQ-016 | Diagnostic events shall carry a simulation timestamp, severity, stable event code, subsystem source, and human-readable message. | JSON schema/serialization test. |

## Scenarios and records

| ID | Requirement | Acceptance evidence |
|---|---|---|
| REQ-017 | The distribution shall provide normal operation, communication interruption, invalid sensor data, overheating, power fluctuation, multiple failures, and temporary-failure recovery scenarios. | Scenario catalog test and `--list-scenarios`. |
| REQ-018 | A run shall write newline-delimited JSON containing one metadata record first, telemetry/event/transition records in simulation order, and one summary record last. | JSONL ordering and parsing integration test. |
| REQ-019 | Every JSONL record shall include `schema_version`, `record_type`, and `timestamp_ms`; schema version 1 shall be used for this release. | Contract test over every emitted record. |
| REQ-020 | `--export-all` shall produce one replay file for every supplied demonstration scenario. | Export-all CLI integration check. |
| REQ-021 | Invalid command-line values and unreadable/malformed scenario input shall produce an actionable diagnostic and non-zero process exit. | CLI error-path tests. |

## Dashboard

| ID | Requirement | Acceptance evidence |
|---|---|---|
| REQ-022 | The dashboard shall read simulator-produced JSONL and shall not calculate independent core health decisions. | Parser/fixture test and architecture review. |
| REQ-023 | The dashboard shall present overall state and individual status for all six fictional subsystems. | Component test with a representative replay. |
| REQ-024 | The dashboard shall provide telemetry charts and a chronological event/transition timeline. | Rendering tests and manual responsive check. |
| REQ-025 | The user shall be able to filter diagnostics by severity and subsystem. | Interaction tests. |
| REQ-026 | A chosen replay shall start at virtual time zero, loop automatically at its recorded duration, and reset immediately when the user chooses another run; replay timing shall be automatic rather than user-controlled. | Replay-hook and run-selection tests. |
| REQ-027 | The dashboard shall show run statistics, an architecture overview, and an exportable report derived from the selected replay. | Component and report-export tests. |
| REQ-028 | Corrupt or unsupported replay data shall result in a visible, recoverable error rather than an unhandled page failure. | Parser and error-boundary tests. |

## Portability, quality, and scope

| ID | Requirement | Acceptance evidence |
|---|---|---|
| REQ-029 | The C++ project shall configure, build, and run tests with MSVC on Windows and GCC or Clang on Linux. | GitHub Actions matrix result. |
| REQ-030 | Native, frontend, and API checks shall be automated in CI; compatible Linux builds shall include sanitizer execution and `clang-tidy` static analysis. | Workflow definition and observed CI result. |
| REQ-031 | A Docker workflow shall reproduce simulator data and start the API and dashboard without requiring host C++, Node.js, or .NET toolchains. | Clean Docker build and stack smoke test. |
| REQ-032 | Documentation and UI shall identify AeroGuard as an independent educational simulation using fictional systems and shall make no certification, real-time, or proprietary-system claim. | Documentation/UI review. |

## Archive, access, and fleet management

| ID | Requirement | Acceptance evidence |
|---|---|---|
| REQ-033 | The API shall validate and import each simulator artifact into SQLite once by its SHA-256 value and preserve the original replay bytes and record order. | Importer and repository integration tests. |
| REQ-034 | The API shall expose public scenario, run, replay, and filtered-record REST resources without recalculating simulator decisions. | In-process HTTP integration tests and architecture review. |
| REQ-035 | The first administrator shall be created only from runtime configuration; no administrator password or reusable token shall be stored in source control. | Bootstrap tests, configuration review, and secret scan. |
| REQ-036 | Login shall issue a time-limited opaque bearer token whose hash is stored in SQLite; logout, password reset, and deactivation shall revoke affected sessions. | Authentication integration tests. |
| REQ-037 | Administrator routes shall require the `Admin` role and shall prevent deletion of built-in roles, removal of the final active Admin role, or deactivation of the current administrator. | Authorization and invariant integration tests. |
| REQ-038 | Administrators shall see all aircraft; other users shall see only aircraft assigned to their account. | Viewer visibility integration tests. |
| REQ-039 | An `Admin` or `Operator` shall be able to create aircraft, and an Admin, `OWNER`, or `MAINTAINER` shall be able to update permitted aircraft using optimistic version checks. | Fleet permission and stale-update tests. |
| REQ-040 | Administrators shall manage `OWNER`, `MAINTAINER`, and `VIEWER` aircraft assignments while at least one owner remains. | Assignment and final-owner tests. |
| REQ-041 | Account, role, session, aircraft, and assignment mutations shall create queryable audit records with actor, action, entity, details, and timestamp. | Audit endpoint integration test. |
| REQ-042 | The dashboard shall prefer the REST archive, fall back to packaged replay files when the API is unavailable, and keep access tokens only in browser memory. | API-client and workspace component tests plus storage review. |

## Non-functional engineering constraints

Code review and structural tests enforce these constraints. They do not claim
hard real-time guarantees:

- Own resources through RAII and make ownership visible in types.
- Prefer values and references; use smart pointers only for genuine ownership
  transfer or polymorphic lifetime.
- Keep the per-tick path bounded and avoid avoidable heap allocation.
- Prefer immutable messages and explicit state ownership over shared mutable
  global state.
- Preserve const-correct APIs and compile with a strict warning set.
- Keep seeded simulation independent of CPU speed and wall-clock scheduling.
