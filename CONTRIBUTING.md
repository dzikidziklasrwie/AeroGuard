# Contributing to AeroGuard

Contributions must keep this educational simulator deterministic, reviewable,
and separate from any real aircraft or defence system.

## Scope and safety boundary

AeroGuard accepts fictional subsystem models, synthetic telemetry, diagnostic
features, tests, and documentation. Do not contribute confidential or
proprietary material, real platform data, weapon or countermeasure behaviour,
tactical threat processing, or code intended to control physical equipment.
The project makes no real-time, safety, or aviation-certification claim.

## Local quality gate

From the repository root, run the native, API, and frontend checks before
opening a pull request:

```bash
cmake --preset debug
cmake --build --preset debug
ctest --preset debug --output-on-failure

dotnet restore backend/AeroGuard.Backend.sln
dotnet build backend/AeroGuard.Backend.sln --configuration Release --no-restore
dotnet test backend/AeroGuard.Backend.sln --configuration Release --no-build
dotnet format backend/AeroGuard.Backend.sln --verify-no-changes --no-restore

cd frontend
npm ci
npm run lint
npm run typecheck
npm test -- --run
npm run build
npm audit --audit-level=moderate
```

On Linux, also run the sanitizer preset when your toolchain supports it:

```bash
cmake --preset sanitizers
cmake --build --preset sanitizers
ctest --preset sanitizers --output-on-failure
```

See [the build guide](docs/build-and-run.md) for prerequisites and platform
details.

## Change guidelines

- Add or update a `REQ-xxx` entry for externally visible behaviour.
- Link each behavioural requirement to an automated test in
  `docs/traceability.md`.
- Keep seeded runs reproducible; tests must not depend on wall-clock time.
- Prefer bounded containers and explicit ownership in the processing path.
- Treat JSONL as a versioned interface and update the dashboard contract tests
  when its schema changes.
- Preserve final-administrator, final-owner, session-revocation, and optimistic
  version invariants when changing account or fleet code.
- Never commit passwords, bearer tokens, local databases, `.env`, or generated
  CV files.
- Never report a test, coverage, sanitizer, or CI result unless you observed it.
- Update `TASKS.md` and relevant documentation in the same change.

Use focused commits and explain the reason for an architectural change, not
only the mechanical edit. Open pull requests in the [AeroGuard repository](https://github.com/dzikidziklasrwie/AeroGuard)
and include the commands you ran and any remaining limitations.
