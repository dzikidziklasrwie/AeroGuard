# Known limitations and roadmap

## Current limitations

- AeroGuard has educational scope. Its models and thresholds are fictional and
  lack validation against an aircraft or environmental test programme.
- AeroGuard makes no certification or hard real-time claim. Virtual-time
  reproducibility says nothing about worst-case execution time,
  schedulability, redundancy, or compliance with DO-178C, DO-254, ARP4754A,
  or related standards.
- The dashboard replays completed JSONL files and animates simulated map
  flights from archived runs. It does not subscribe to a running embedded
  target or any aircraft feed.
- The physical models support fault-handling demonstrations, not high-fidelity
  electrical, thermal, or communications simulation.
- The single-process reference path does not model distributed clocks, packet
  reordering, partial network partitions, or process restarts.
- Code validates schema version 1, but the project has no formal JSON Schema
  document or multi-version migration layer.
- Code validates the expected scenario and replay structure, but the parser is
  not hardened for adversarial input and does not run in a sandbox.
- SQLite, the opaque-session store, and the in-memory SignalR connection
  registry target one API instance. The project has no database migration
  history, replication, SignalR backplane, external identity provider,
  multi-factor authentication, account recovery, or distributed session
  invalidation.
- The broadcaster creates permission-aware snapshots per recipient. It isolates
  one flight's failure from the rest of a cycle, but the design targets a small
  demonstrator rather than a distributed or high-volume fleet.
- Each active flight retains its latest 500 chat messages in SQLite until the
  flight is deleted. There is no separate age-based retention or
  message-archive policy.
- The API and nginx settings are suitable for local review. An internet-facing
  deployment would still need managed TLS, secret storage, backup and restore,
  telemetry, alerting, deployment-specific retention rules, abuse testing, and
  an operations plan.
- The dashboard keeps its access token only in memory. A page refresh signs the
  user out by design; there is no browser persistence or refresh-token flow.
- A workflow file does not prove that a GitHub-hosted run has passed. Check the
  hosted run before reporting CI status.
- A local headless-Chrome check exercised the browser, nginx WebSocket upgrade,
  and Docker network path together. This path is not yet an automated test or a
  hosted-CI result.

## Prioritized future work

1. Add a formal JSON Schema and producer/consumer compatibility job.
2. Add measured native coverage reporting with documented exclusions - never a
   guessed percentage.
3. Add property-based tests for telemetry boundary combinations and state
   transition sequences.
4. Add fuzz targets for scenario parsing and JSONL ingestion.
5. Connect a running C++ producer through a back-pressured local transport
   while retaining file replay for deterministic tests. Current SignalR
   updates select archived records rather than streaming from the producer.
6. Benchmark allocation counts and per-tick execution distributions on a named
   reference machine; keep the results separate from hard real-time claims.
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
