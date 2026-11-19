# Test strategy

## Objectives

Tests cover deterministic behaviour, boundary conditions, and failure handling.
Every result cites an observed command or artifact. This document does not use
invented coverage percentages or unverified claims.

## Test layers

| Layer | Scope | Typical evidence |
|---|---|---|
| C++ unit | State policy, validation, bounded queue, clock, parsing, serialization | Fast GoogleTest cases that avoid filesystem and wall-clock dependencies |
| C++ integration | Engine + simulated HAL + scenario + JSONL sink | Tests run seeded simulations in temporary files and parse the records |
| CLI smoke | Argument validation, listing, one run, export-all | Process exit code and generated artifact checks |
| Frontend unit/component | JSONL parser, automatic replay cursor, filtering, status and error states | Vitest + React Testing Library in jsdom |
| Contract | C++ schema producer against dashboard fixtures/parser | Versioned representative JSONL and required-field assertions |
| API integration | SQLite import, REST resources, authentication, authorization, fleet rules, and audit | xUnit through `WebApplicationFactory` with a temporary database |
| CI portability | MSVC and GCC/Clang build/test paths | GitHub Actions job results |
| Dynamic/static analysis | Memory/undefined behaviour and maintainability defects | Linux sanitizer preset and static-analysis job |
| Manual exploratory | Responsive layout, keyboard use, readability, report download | Reviewers record the browser and viewport in a pull-request checklist |

## High-risk test design

### State transitions

Use a table-driven test over every source/destination pair. Assert acceptance
for all eleven allow-listed edges, rejection for every disallowed edge, no-op
behaviour for same-state observations, unchanged state on rejection, and exact
transition-record ordering.

### Telemetry validation

Partition each floating-point measurement into below-minimum, boundary,
nominal, above-maximum, `NaN`, and positive/negative infinity. Also test missing
sources, stale/duplicate sequences, and malformed scenario fields. Invalid input
must not crash or silently become healthy.

### Bounded queue

Test zero/unsupported capacity construction, FIFO order, empty pop, fill to the
exact capacity, one additional push, and reuse after pop. Where concurrency is
part of the implementation, use bounded deterministic coordination rather than
timing-based sleeps.

### Heartbeats and recovery

Drive a fake/configurable clock to immediately before, at, and after the
timeout. Assert that the monitor detects one loss, suppresses duplicate events
on repeated ticks, observes a resumed heartbeat, and follows the documented
recovery path.

### Deterministic scenarios

Run each scenario twice with identical configuration and compare ordered
records byte-for-byte. Run a second seed for scenarios containing randomized
variation and assert that their telemetry differs while remaining valid.

### Dashboard contract and automatic replay

Cover CRLF/LF, blank lines, malformed JSON, unknown record types, unsupported
schema versions, empty files, and representative full runs. Use fake timers to
verify automatic start, end-to-start looping, and reset after a new run is
selected so browser speed cannot make tests flaky.

### Access and fleet rules

Use the in-process HTTP server with a new temporary SQLite database. Cover
generic login failure, bearer challenges, logout, password-reset and
deactivation revocation, final-administrator protection, built-in role
protection, assignment visibility, role-based write access, unique tail
numbers, stale versions, final-owner protection, and audit records. Never use
a production database or a checked-in credential in tests.

## Local commands

```bash
cmake --preset debug
cmake --build --preset debug
ctest --preset debug --output-on-failure
```

```bash
cd frontend
npm ci
npm run lint
npm run typecheck
npm test -- --run
npm run build
npm audit --audit-level=moderate
```

```bash
dotnet restore backend/AeroGuard.Backend.sln
dotnet build backend/AeroGuard.Backend.sln --configuration Release --no-restore
dotnet test backend/AeroGuard.Backend.sln --configuration Release --no-build
dotnet format backend/AeroGuard.Backend.sln --verify-no-changes --no-restore
```

On a compatible Linux toolchain:

```bash
cmake --preset sanitizers
cmake --build --preset sanitizers
ctest --preset sanitizers --output-on-failure
```

## CI evidence policy

- Don't report a configured workflow as passing until GitHub has run it.
- Report a local pass with the OS, compiler or runtime, command, and date.
- Show and explain skipped tests; don't count them as passes.
- Report coverage only when a named tool generated it from that revision.
- Retain generated JSONL and the built dashboard as CI artifacts for failure
  inspection.
- Treat security invariants as HTTP-level tests so routing, middleware,
  authentication, services, repositories, and SQLite constraints are covered
  together.

The 2026-11-19 local snapshot includes 44/44 native tests on both MSVC and GCC,
44/44 under GCC ASan/UBSan, a completed Clang 18 clang-tidy build, and 31/31
.NET integration tests, plus 25/25 Vitest tests in 7 files.
[TASKS.md](../TASKS.md) records frontend lint, typecheck, production build,
dependency audit, exact tool versions, Docker smoke-test evidence, and
remaining hosted checks. [The traceability matrix](traceability.md) maps
requirements to test cases.
