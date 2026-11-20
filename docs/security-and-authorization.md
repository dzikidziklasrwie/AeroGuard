# Security and authorization rules

## Scope

AeroGuard is a local educational simulation. It stores synthetic flight
positions, fictional subsystem telemetry, account data, chat messages, and
audit records. It does not connect to aircraft or claim certification,
airworthiness, hard real-time deadlines, or suitability for physical control.

The main trust boundaries are the browser-to-nginx connection, the nginx-to-API
proxy, the API-to-SQLite connection, and the import boundary between C++ JSONL
artifacts and SQLite. Natural Earth geometry ships with the frontend, so the
map does not send positions or viewport requests to a tile provider.

## Roles, assignments, and operation permissions

Roles and aircraft assignments answer different questions. A role supplies
application-wide defaults. An assignment decides which aircraft a user can
see and whether that user owns it.

| Action | `Admin` | Assigned non-owner | `OWNER` |
|---|---|---|---|
| View flight, telemetry, and chat | Every active flight | Assigned aircraft | Assigned aircraft |
| Place an aircraft | Any `ACTIVE` aircraft | Denied | Requires `FLIGHT_PLACE_OWN` |
| Reconfigure a flight | Any active flight | Denied | Requires `FLIGHT_RECONFIGURE_OWN` |
| Remove a flight | Any active flight | Denied | Requires `FLIGHT_REMOVE_OWN` |
| Send text or location | Allowed | Requires `CHAT_SEND` | Requires `CHAT_SEND` |
| Send telemetry | Allowed | Denied | Requires `CHAT_SEND` and `TELEMETRY_SHARE` |
| Replace operation permissions | Allowed | Denied | Denied |

`Admin` bypasses all five operation checks. With no override rows, `Operator`
receives all five permissions and `Viewer` receives `CHAT_SEND`. A user with
custom roles only receives no operation permission. Once an administrator
saves an override set, that complete set replaces role defaults. Saving no
checked permissions creates an explicit empty override.

Any active assigned user can read a flight even without an operation
permission. An unassigned user receives a not-found response for that flight
and its messages. This avoids disclosing whether another account's flight ID
exists.

The map shows one primary owner label. The API chooses the earliest `OWNER`
assignment by assignment time, then user ID. Aircraft assignment routes remain
the source for the full owner and co-owner list.

## Sessions and SignalR

Login returns a random opaque bearer token. SQLite stores its SHA-256 value,
expiry, user link, and revocation time. The browser keeps the plain token in
memory and sends it in the `Authorization` header for REST requests.

The SignalR browser client may send the token through the `access_token` query
parameter because browser WebSocket APIs cannot set the authorization header.
The authentication handler accepts that parameter only under
`/hubs/operations`. nginx disables access logging for `/hubs/` so the query
value does not enter its normal request log.

Each hub connection registers its user and session IDs in an in-memory
connection registry. Before each broadcast, the API checks that the session is
active and that the account still has the required role or aircraft
assignment. Logout, password reset, configured administrator password
synchronization, account deactivation, and session expiry stop later delivery.
Role and assignment changes alter the flight recipients.
The browser reloads the REST snapshot after SignalR reconnects so a missed
removal cannot leave a stale marker.

The registry exists in one API process. Multiple API replicas would need a
shared SignalR backplane and shared connection/session coordination.

## Server-owned data and validation

The server sets message sender, display name, timestamp, and shared payload.
Clients submit message text and a share type but cannot supply another user's
identity or arbitrary telemetry. React renders message text without injecting
HTML.

Flight placement validates the aircraft, assignment, operation permission,
archived run, route scope, finite coordinates, speed, and uniqueness. Updates
also require the current version. `WORLD` is the canonical code for a world
route. Continent routes accept `AF`, `AN`, `AS`, `EU`, `NA`, `OC`, or `SA`;
country routes require an ISO 3166-1 alpha-3 code.

An aircraft with an active flight must remain `ACTIVE`. The API rejects a
status change or aircraft deletion until an authorized user removes the flight.
Removing an aircraft assignment immediately removes that user's REST visibility
and later SignalR delivery.

Chat accepts 1 to 500 characters and returns at most 200 recent messages per
request. Each active flight retains its latest 500 messages. Those rows stay in
SQLite until the flight is deleted, when the database removes them through the
flight foreign-key cascade. The insert statement rechecks active-account,
assignment, `CHAT_SEND`, owner, and `TELEMETRY_SHARE` rules as applicable, so a
permission or assignment revocation cannot race between authorization and the
write.

## Audit and deployment limits

The audit log records account, role, session, aircraft, assignment,
operation-permission, and active-flight mutations with actor, entity, details,
and time. Chat rows contain sender and time but do not create duplicate audit
entries. The log supports local review; it is not tamper-evident.

Fixed-window limits allow 30 login requests per source address per minute, 30
hub-negotiation requests per account per minute, 90 flight writes per account
per minute, and 60 chat writes per account per minute. Authentication runs
before the account-based limiter chooses its partition. A public deployment
still needs TLS, managed secret storage, a deployment-specific
message-retention policy, backup and restore, service monitoring, and tested
capacity limits. Compose binds the API and dashboard to loopback on ports 8081
and 8080, permits weak passwords only in its Development profile, and enables
one-hop forwarded-header processing for the nginx proxy. SQLite, the one-second flight broadcaster,
and the in-memory connection registry target a single local API instance. The
broadcaster isolates a failed flight from the remaining flights in that cycle,
but it builds recipient-specific snapshots. This is appropriate for a small
demonstrator, not a distributed or high-volume fleet.
