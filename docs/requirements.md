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
| REQ-032 | Documentation and UI shall identify AeroGuard as independent educational software using fictional systems. They shall make no certification, hard real-time deadline, live-aircraft connection, operational-use, or proprietary-system claim. | Documentation and UI review. |

## Archive, access, and fleet management

| ID | Requirement | Acceptance evidence |
|---|---|---|
| REQ-033 | The API shall validate and import each simulator artifact into SQLite once by its SHA-256 value and preserve the original replay bytes and record order. | Importer and repository integration tests. |
| REQ-034 | The API shall expose public scenario, run, replay, and filtered-record REST resources without recalculating simulator decisions. | In-process HTTP integration tests and architecture review. |
| REQ-035 | The administrator shall be created only from runtime configuration; optional startup synchronization shall update its password and revoke older sessions without storing a plain password or reusable token in source control. | Bootstrap and password-sync tests, configuration review, and secret scan. |
| REQ-036 | Login shall issue a time-limited opaque bearer token whose hash is stored in SQLite; logout, password reset, and deactivation shall revoke affected sessions. | Authentication integration tests. |
| REQ-037 | Administrator routes shall require the `Admin` role and shall prevent deletion of built-in roles, removal of the final active Admin role, or deactivation of the current administrator. | Authorization and invariant integration tests. |
| REQ-038 | Administrators shall see all aircraft; other users shall see only aircraft assigned to their account. | Viewer visibility integration tests. |
| REQ-039 | An `Admin` or `Operator` shall be able to create aircraft, and an Admin, `OWNER`, or `MAINTAINER` shall be able to update permitted aircraft using optimistic version checks. | Fleet permission and stale-update tests. |
| REQ-040 | Administrators shall manage `OWNER`, `MAINTAINER`, and `VIEWER` aircraft assignments while at least one owner remains. | Assignment and final-owner tests. |
| REQ-041 | Account, role, session, aircraft, assignment, operation-permission, and active-flight mutations shall create queryable audit records with actor, action, entity, details, and timestamp. | Audit endpoint integration tests. |
| REQ-042 | The recorded-run dashboard shall prefer the REST archive, fall back to packaged replay files when the API is unavailable, and keep access tokens only in browser memory. | API-client and workspace component tests plus storage review. |

## Simulated flight operations

| ID | Requirement | Acceptance evidence |
|---|---|---|
| REQ-043 | After sign-in, the dashboard shall open the operations map and show each visible simulated flight's position, heading, speed, route scope, primary owner, scenario clock, condition, and stored C++ telemetry. The client shall use packaged Natural Earth geometry without third-party tile requests. | Map component tests and dependency review. |
| REQ-044 | An administrator shall see every active flight. Any active account assigned to an aircraft shall see that aircraft's flight, chat, and hub events; an unassigned account shall receive none of them. | Visibility and authorization integration tests. |
| REQ-045 | An administrator shall place any `ACTIVE` aircraft and reconfigure or remove any active flight. A non-administrator shall require an `OWNER` assignment plus the matching `FLIGHT_PLACE_OWN`, `FLIGHT_RECONFIGURE_OWN`, or `FLIGHT_REMOVE_OWN` permission. The API shall limit these writes to 90 requests per account per minute. | Operation-permission and rate-limit integration tests. |
| REQ-046 | Operation permissions shall be per-account overrides. An administrator shall bypass all five checks. With no override rows, `Operator` shall receive all five permissions, `Viewer` shall receive `CHAT_SEND`, and a custom-role-only account shall receive none. Administrators shall replace the override set, including an explicitly empty set, and each replacement shall be audited. | Permission-default, override, and audit tests. |
| REQ-047 | One aircraft shall have at most one active flight. While that flight exists, the aircraft shall remain `ACTIVE` and shall not be deleted. Placement and reconfiguration shall require an `ACTIVE` aircraft, an archived run, finite coordinates, latitude from -85 to 85, longitude from -180 to 180, speed from 400 to 1100 km/h, and the current version for updates. `WORLD` shall be the canonical world-scope code; continent routes shall use the seven documented codes and country routes shall use ISO 3166-1 alpha-3 codes. | Boundary, status, deletion, country-code, uniqueness, and stale-version integration tests. |
| REQ-048 | The API shall compute accelerated synthetic route motion independently from scenario playback. Scenario telemetry shall advance at wall-clock speed, select stored C++ records, and loop at the recorded duration. The API and browser shall not evaluate sensor thresholds or state transitions. | Controlled-time motion tests and architecture review. |
| REQ-049 | The browser shall load a REST snapshot before accepting authenticated SignalR updates at a nominal one-second cadence. It shall reject a flight update or removal whose event sequence is not newer than the last accepted event for that flight, and shall reload the REST snapshot after reconnection. Assignment, role, permission, account-status, and session-revocation changes shall affect later deliveries. Failure to prepare one flight update shall not stop the other flights in that broadcast cycle. Hub negotiation shall be limited to 30 requests per account per minute. The feature shall make no delivery-deadline claim. | Hub authentication, recipient, revocation, event-ordering, failure-isolation, reconnect, and rate-limit tests or review. |
| REQ-050 | Assigned users shall read flight chat. `CHAT_SEND` shall permit text and location messages. Telemetry sharing shall also require `OWNER` and `TELEMETRY_SHARE`; administrators shall bypass these checks. Message insertion shall recheck these rules in the same SQL statement that writes the row. The server shall set sender and time, build location and telemetry payloads, accept messages from 1 to 500 characters, retain the latest 500 messages per active flight, and limit message writes to 60 requests per account per minute. | Chat authorization, atomic-write, validation, retention, and rate-limit integration or storage tests. |
| REQ-051 | Every packaged scenario shall default to 40,000 through 55,000 virtual milliseconds, preserve its documented fault and recovery path, remain deterministic for the same configuration, and agree with exported manifests and records. | Scenario-duration, export, importer, and frontend contract tests. |
| REQ-052 | When the operations API is unavailable because of a network, 404, or server error, the map may show a clearly labelled, memory-only session demo. It shall not claim persistence, API state, or live-aircraft data. Authentication failure shall sign the user out instead of entering session-demo mode. | Fallback and authentication component tests. |

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
