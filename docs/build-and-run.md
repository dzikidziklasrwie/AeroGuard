# Build, install, and run

## Prerequisites

| Tool | Minimum / supported path | Notes |
|---|---|---|
| C++ compiler | MSVC 19.3x+, GCC 12+, or Clang 15+ with C++20 | A completed CI run is the portability reference |
| CMake | 3.24 | Presets are version 6 |
| Ninja | Available on `PATH` | Used by every supplied preset |
| Git/network | Network access on first configure | CMake fetches nlohmann/json and GoogleTest as needed when compatible local packages don't exist |
| Node.js | 20.19+, 22.13+, or 24+ | Dashboard development only; required by the locked lint toolchain |
| npm | Bundled with Node | Uses the checked-in lockfile |
| .NET SDK | 10.0 | API development and xUnit integration tests |
| Docker Desktop/Engine | Docker Engine with Compose v2 | Optional, no host compiler needed |

## Linux native build

Install a compiler, CMake, and Ninja with your distribution package manager.
From the repository root:

```bash
cmake --preset debug
cmake --build --preset debug
ctest --preset debug --output-on-failure
```

Build an optimized binary with:

```bash
cmake --preset release
cmake --build --preset release
```

The executables are `build/debug/aeroguard` and
`build/release/aeroguard` for the corresponding Ninja preset.

## Windows native build

Install Visual Studio 2022 with the `Desktop development with C++` workload,
plus CMake and Ninja. Open `Developer PowerShell for VS 2022` so `cl.exe` is available,
then run from the repository root:

```powershell
cmake --preset debug
cmake --build --preset debug
ctest --preset debug --output-on-failure
```

The Ninja preset produces `build\debug\aeroguard.exe`. If CMake previously
configured the directory with a different compiler, remove only that build
preset directory and configure again; do not mix compiler caches.

## Simulator CLI

Print the current CLI options and supplied scenarios:

```bash
./build/debug/aeroguard --help
./build/debug/aeroguard --list-scenarios
```

Run a reproducible scenario:

```bash
mkdir -p demo-output
./build/debug/aeroguard \
  --scenario simulated-overheating \
  --seed 42 \
  --duration 20000 \
  --tick 100 \
  --output demo-output/overheating.jsonl
```

PowerShell equivalent:

```powershell
New-Item -ItemType Directory -Force demo-output | Out-Null
.\build\debug\aeroguard.exe `
  --scenario simulated-overheating `
  --seed 42 `
  --duration 20000 `
  --tick 100 `
  --output demo-output\overheating.jsonl
```

`--scenario` accepts a supplied slug or a scenario-file path. Export all
dashboard replays with:

```bash
./build/debug/aeroguard \
  --export-all frontend/public/data \
  --scenarios-dir scenarios \
  --seed 42
```

The command writes seven `.jsonl` files and `manifest.json`. A repeated run
with the same effective scenario, seed, duration, and tick has deterministic
simulation output.

## Dashboard

First generate or retain simulator data under `frontend/public/data`, then:

```bash
cd frontend
npm ci
npm run dev
```

Open the URL printed by Vite (normally `http://localhost:5173`). Vite proxies
`/api` to `http://localhost:5080`. The UI tries the REST archive first and
falls back to `/data/manifest.json` when the API is unavailable. Opening
`index.html` directly from disk doesn't provide the required fetch behaviour.

Production and test commands:

```bash
npm run lint
npm run typecheck
npm test -- --run
npm run build
npm audit --audit-level=moderate
npm run preview
```

`npm run preview` serves the production bundle for inspection; it is not a
production server recommendation.

## API and local accounts

Set an administrator password outside the repository and start the API:

PowerShell:

```powershell
$env:AEROGUARD_ADMIN_PASSWORD = "choose-a-long-local-password"
dotnet run --project backend/src/AeroGuard.Api --urls http://localhost:5080
```

Bash:

```bash
export AEROGUARD_ADMIN_PASSWORD='choose-a-long-local-password'
dotnet run --project backend/src/AeroGuard.Api --urls http://localhost:5080
```

The first start creates the `admin` account. The plain password is not written
to SQLite or Git. The API imports the seven packaged replays, exposes OpenAPI
at `http://localhost:5080/openapi/v1.json`, and stores local data under its
ignored `.data` directory.

For the exact local-only `admin` / `admin` demonstration requested by some
reviewers, weak bootstrap passwords require an explicit Development setting:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:AEROGUARD_ALLOW_WEAK_DEVELOPMENT_PASSWORD = "true"
$env:AEROGUARD_ADMIN_PASSWORD = "admin"
dotnet run --project backend/src/AeroGuard.Api --urls http://localhost:5080
```

Don't use that mode on a shared machine or deployment. Production rejects the
weak-password switch. See the [API guide](../backend/README.md) for the route,
role, session, and aircraft rules.

Verify the API with:

```bash
dotnet restore backend/AeroGuard.Backend.sln
dotnet build backend/AeroGuard.Backend.sln --configuration Release --no-restore
dotnet test backend/AeroGuard.Backend.sln --configuration Release --no-build
dotnet format backend/AeroGuard.Backend.sln --verify-no-changes --no-restore
```

## Sanitizers and static analysis

The non-Windows sanitizer preset enables AddressSanitizer and
UndefinedBehaviorSanitizer:

```bash
cmake --preset sanitizers
cmake --build --preset sanitizers
ctest --preset sanitizers --output-on-failure
```

For Clang-based local analysis:

```bash
cmake --preset debug -DAEROGUARD_ENABLE_CLANG_TIDY=ON
cmake --build --preset debug
```

Set `-DAEROGUARD_WARNINGS_AS_ERRORS=ON` to treat warnings as errors. The CI
workflow uses strict warnings and runs supported analysis separately so each
platform's limitations remain visible.

## Install

After configuring and building `release`, choose a prefix and install:

```bash
cmake --install build/release --prefix ./install
```

CMake installs the executable to `install/bin` and the supplied scenarios to
`install/share/aeroguard/scenarios`. Pass that directory with `--scenarios-dir`
when exporting installed scenarios.

## Docker demonstration

Copy the environment template, replace its password, then build the integrated
API and dashboard. The C++ binary generates replay data during the image build:

```bash
cp .env.example .env
docker compose up --build
```

PowerShell equivalent:

```powershell
Copy-Item .env.example .env
# Edit .env and replace AEROGUARD_ADMIN_PASSWORD before continuing.
docker compose up --build
```

Open `http://localhost:8080`. Direct API access is available at
`http://localhost:8081`. SQLite uses the `aeroguard-data` named volume. Stop
the stack with `Ctrl+C`, then `docker compose down`. Add `--volumes` only when
you intentionally want to remove the local database.

Run the simulator image directly and persist fresh files to `demo-output`:

```bash
docker compose run --rm simulator \
  --export-all /data \
  --scenarios-dir /opt/aeroguard/scenarios \
  --seed 42
```

## Troubleshooting

| Symptom | Cause / action |
|---|---|
| CMake cannot download a dependency | Confirm network/proxy access, or install compatible nlohmann/json and GoogleTest packages locally. |
| `cl.exe` is not found | Use a Visual Studio Developer PowerShell, then reconfigure `build/debug`. |
| `ninja` is not found | Install Ninja and ensure it is on `PATH`. |
| Dashboard says the manifest is unavailable | Generate data into `frontend/public/data` and use the Vite server, not `file://`. |
| Login says the username or password is incorrect | Set `AEROGUARD_ADMIN_PASSWORD` before the first API start. If the database already exists, use its current password or intentionally create a new local database. |
| Vite shows static replay data instead of the REST archive | Start the API on port 5080 and confirm `GET /api/health` succeeds through the Vite proxy. |
| Dashboard rejects a replay | Confirm schema version 1 and inspect the dashboard's line-specific parse warnings. |
| Sanitizer preset is unavailable on Windows | This is intentional; run it with GCC/Clang on Linux or WSL. |
