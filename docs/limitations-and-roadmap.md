# Known limitations and roadmap

## Current limitations

- AeroGuard has educational scope. Its models and thresholds are fictional and
  lack validation against an aircraft or environmental test programme.
- AeroGuard makes no certification or real-time claim. Virtual-time
  reproducibility says nothing about worst-case execution time,
  schedulability, redundancy, or compliance with DO-178C, DO-254, ARP4754A,
  or related standards.
- The dashboard replays completed JSONL files. It does not subscribe to a
  running embedded target.
- The physical models support fault-handling demonstrations, not high-fidelity
  electrical, thermal, or communications simulation.
- The single-process reference path does not model distributed clocks, packet
  reordering, partial network partitions, or process restarts.
- Code validates schema version 1, but the project has no formal JSON Schema
  document or multi-version migration layer.
- Code validates the expected scenario and replay structure, but the parser is
  not hardened for adversarial input and does not run in a sandbox.
- SQLite and the opaque-session store target a single demonstration instance.
  The project has no database migration history, replication, external identity
  provider, multi-factor authentication, account recovery, or distributed
  session invalidation.
- The API and nginx settings are suitable for local review. An internet-facing
  deployment would still need managed TLS, secret storage, backup and restore,
  telemetry, alerting, retention rules, abuse testing, and an operations plan.
- The dashboard keeps its access token only in memory. A page refresh signs the
  user out by design; there is no browser persistence or refresh-token flow.
- A workflow file does not prove that a GitHub-hosted run has passed. Check the
  hosted run before reporting CI status.

## Prioritized future work

1. Add a formal JSON Schema and producer/consumer compatibility job.
2. Add measured native coverage reporting with documented exclusions - never a
   guessed percentage.
3. Add property-based tests for telemetry boundary combinations and state
   transition sequences.
4. Add fuzz targets for scenario parsing and JSONL ingestion.
5. Introduce a live, back-pressured local transport behind the same record
   interface while retaining file replay for deterministic tests.
6. Benchmark allocation counts and per-tick execution distributions on a named
   reference machine; keep the results separate from real-time claims.
7. Exercise ARM cross-compilation and a constrained target profile with no
   dynamic allocation after initialization.
8. Sign release artifacts and produce an SBOM/dependency audit.
9. Add versioned database migrations, encrypted backup/restore checks, and a
   supported PostgreSQL deployment profile.
10. Integrate an external OpenID Connect provider, multi-factor authentication,
    and short-lived refresh-token rotation for a hosted deployment.
11. Improve dashboard accessibility through keyboard, contrast, screen-reader,
    and reduced-motion audits.
12. Add scenario authoring validation and a guided editor without moving health
    logic into the frontend.

## Requirements for a safety-related product

A real safety-related programme starts with system safety analysis, independent
requirements baselines, tool qualification decisions, bidirectional
traceability, configuration control, review independence, target hardware
evidence, timing and resource budgets, and a certification plan. AeroGuard
demonstrates a subset of these engineering practices but does not replace any
of those activities.
