# Design decisions and trade-offs

These records give reviewers and interviewers the rationale behind the current
architecture. They don't define universal rules for production avionics.

## DD-001 - Virtual time instead of wall-clock scheduling

Decision. The engine receives an `ISimulationClock`; the desktop simulator
uses a manually advanced clock.

Reason. Tests reach timeout boundaries instantly, scenario duration does
not slow CI, and output does not vary with CPU scheduling.

Trade-off. This demonstrates logical determinism, not timing determinism or
deadline compliance. A real scheduler integration requires separate timing,
priority, and worst-case execution analysis.

## DD-002 - Closed typed telemetry variant

Decision. `TelemetryPayload` is a `std::variant` of six subsystem-specific
structs and each message carries a strongly typed `SubsystemId`.

Reason. The compiler knows every valid payload family, visitation is explicit,
and ownership stays value-based without a polymorphic allocation per sample.

Trade-off. Adding a subsystem requires updating visitors and serializers. That
required update makes an expansion of the closed interface visible in review.
A plugin ecosystem requires a different extension boundary.

## DD-003 - Compile-time bounded queue

Decision. `BoundedQueue<T, Capacity>` uses a fixed `std::array` ring with
explicit failure/timeout behaviour and internal synchronization.

Reason. The capacity template argument declares maximum slot storage, overflow
cannot silently grow memory, and the queue isolates producer/consumer
ownership.

Trade-off. Capacity is a compile-time policy and each slot contains an
`optional<T>`. It is less flexible than a dynamically sized queue and does not
constitute a lock-free or hard real-time queue.

## DD-004 - Synchronous deterministic orchestration

Decision. The main simulation generates and processes each virtual tick in a
stable sequence. The queue provides a thread-safe boundary, but the engine
doesn't create worker threads.

Reason. Concurrency adds schedule-dependent ordering without improving this
file-based desktop demonstration. The project demonstrates deterministic
records and testable health policy, not concurrent throughput.

Trade-off. This does not benchmark concurrent throughput. A threaded model
requires an explicit ordering policy and deterministic integration tests.

## DD-005 - JSON Lines as the producer/consumer seam

Decision. C++ emits versioned JSONL plus a small discovery manifest; React
loads those files as replay input.

Reason. The producer writes one record at a time, developers inspect each line
with ordinary tools, and CI retains the files as artifacts. The format
also keeps health-policy logic out of the UI.

Trade-off. This is replay, not live IPC. JSON is larger and less strongly typed
on the wire than a binary schema. Schema-version validation and contract tests
reject unsupported input, but JSON still relies on runtime field checks.

## DD-006 - Explicit transition allow-list

Decision. The state machine owns valid edges; the health monitor supplies
conditions but cannot assign an arbitrary state.

Reason. Invalid transitions are testable, recovery cannot accidentally bypass
its observation state, and state records have one authority.

Trade-off. Policy changes touch both the allow-list and its exhaustive test
matrix. That duplication is deliberate verification, not accidental logic.

## DD-007 - Dependency injection through narrow interfaces

Decision. The composition root supplies environment-sensitive dependencies,
including the simulation clock and hardware abstraction, to the core.

Reason. Unit tests control time and observations without global hooks or real
devices. Production composition remains visible in `main`/the simulator setup.

Trade-off. Interfaces add indirection. The design avoids a general-purpose
service container because the composition root shows every lifetime and
connection in one place.

## DD-008 - Lightweight frontend dependencies

Decision. React renders the replay dashboard. Plain TypeScript modules and
hooks handle parsing and automatic replay timing, while C++ output remains
authoritative.

Reason. Reviewers can follow parsing and replay timing without tracing through
a charting framework, and the build produces static files for deployment.

Trade-off. Custom visualization code carries accessibility and rendering
responsibility that an established chart library otherwise provides.

## DD-009 - FetchContent with system-package preference

Decision. CMake first searches for compatible nlohmann/json and GoogleTest,
then downloads pinned releases when they are unavailable.

Reason. A new contributor configures without installing each dependency
manually, while offline or package-managed environments supply vetted packages.

Trade-off. The first default configure needs network access and third-party
download integrity remains part of the supply-chain surface. Release or
regulated development requires stricter dependency pinning and provenance
audits.

## DD-010 - SQLite archive behind a REST boundary

Decision. ASP.NET Core imports completed JSONL artifacts into SQLite and
exposes replay, record, account, and fleet resources through Minimal API
routes. The original replay bytes and SHA-256 value remain stored with parsed
records.

Reason. The browser gains queryable run history and relational data without
moving health decisions out of C++. Idempotent import makes process restarts
safe for the same artifact set.

Trade-off. SQLite keeps local setup small but targets one service instance.
Higher write volume or multiple API replicas would require a server database
and versioned migrations.

## DD-011 - Revocable opaque sessions

Decision. Login returns a random 256-bit bearer value. The database stores its
SHA-256 value, expiry, user link, and optional revocation time. The browser
keeps the plain token only in memory.

Reason. Logout, password reset, and deactivation take effect on the next
request. A copied database does not contain reusable bearer values.

Trade-off. Every protected request reads the session store, and a browser
refresh requires another login. A distributed deployment would need shared
session storage or a different token design.

## DD-012 - Endpoint, service, and repository boundaries

Decision. Minimal API endpoint classes translate HTTP requests, application
services enforce permissions and invariants, and repositories contain
parameterized SQLite operations. DTOs do not expose password hashes or stored
token hashes.

Reason. Each layer has one primary reason to change. Permission rules can be
tested through HTTP while storage details remain replaceable behind narrow
interfaces.

Trade-off. The small application has more files and mappings than direct SQL
inside route handlers. The separation pays for itself once account and fleet
rules overlap.

## DD-013 - Optimistic aircraft updates

Decision. Every aircraft record has an integer version. An update supplies
`expected_version`, and SQL changes the row only when the stored version still
matches.

Reason. Two operators cannot silently overwrite each other's changes. A stale
client receives HTTP 409 and must reload before retrying.

Trade-off. Clients must retain the returned version and handle conflicts. The
rule protects one record; it does not provide a multi-record workflow lock.

## DD-014 - Stored telemetry with separate map motion

Decision. Active flights reference an archived C++ run. The API advances that
run at wall-clock speed and loops it at its recorded duration. A separate
calculation moves the marker around a synthetic route at an accelerated display
rate.

Reason. A 40 to 55 second scenario remains readable while the marker moves far
enough to see on a world map. C++ remains the source for telemetry validation,
subsystem health, and state transitions.

Trade-off. The marker is a presentation simulation. It does not represent a
recorded or observed aircraft track, and the one-second update cadence is not a
deadline guarantee.

## DD-015 - REST snapshot before SignalR updates

Decision. The browser loads an authorized REST snapshot, then accepts
`flight_updated`, `flight_removed`, and `message_posted` events from an
authenticated SignalR connection. Per-flight sequences reject older delayed
events. The browser reloads the snapshot after reconnecting.

Reason. REST gives the browser a complete starting state. SignalR carries small
changes while connected, the reconnect read repairs an event gap, and a
five-second REST poll keeps the map usable when the hub cannot connect.

Trade-off. The server keeps a process-local connection registry, checks the
backing session before each delivery, and builds a snapshot per recipient. A
failure for one flight doesn't stop the rest of that broadcast cycle. Multiple
API replicas or a high-volume fleet need shared connection coordination, a
SignalR backplane, and a different fan-out design.

## DD-016 - Assignment plus account operation permissions

Decision. Aircraft assignment controls visibility and ownership. Five
per-account permissions control placement, reconfiguration, removal, chat, and
telemetry sharing. `Admin` bypasses the five checks. `Operator` and `Viewer`
provide defaults only while the account has no override rows.

Reason. An administrator can restrict one operator without changing ownership
or creating a new role. Read access stays tied to the aircraft assignment.
Chat insertion repeats the current permission and assignment predicates in its
SQL statement, avoiding a check-then-write revocation race.

Trade-off. Saving an override replaces the complete set, including an
explicitly empty set. The UI and API must show effective permissions so an
administrator can distinguish role defaults from saved account policy.

## DD-017 - Validate coordinates and clip the projected map layer

Decision. The API accepts flight centers only within latitude -85 to 85 and
longitude -180 to 180. `FlightMotion` normalizes every calculated longitude
and bounds every latitude. React repeats the finite-range check before D3
projection and clips aircraft, labels, and placement markers with the Natural
Earth sphere path.

Reason. Coordinate validation protects the data contract, while SVG clipping
protects the rendered geometry. The two checks address different failure
modes: a malformed update must not produce a transform, and a valid marker at
the projection edge must not paint into the surrounding panel.

Trade-off. A route can cross the antimeridian and reappear on the opposite map
edge, which is correct for a world projection but is not a continuous wrapped
track. Scope names choose route scale and descriptive metadata; they do not
claim containment inside political border polygons.
