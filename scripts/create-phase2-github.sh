#!/usr/bin/env bash
# =============================================================================
# Phase 2 GitHub Setup Script — dotnet-forge
# Creates milestones, issues, and a project for Phase 2 (Manufacturing Hooks)
#
# Prerequisites:
#   - gh CLI installed and authenticated: gh auth login
#   - Run from inside the dotnet-forge repo directory
#   - The user running this must have write access to southwestmogrown/dotnet-forge
#
# Usage:
#   chmod +x create-phase2-github.sh
#   ./create-phase2-github.sh
# =============================================================================
set -euo pipefail

REPO="southwestmogrown/dotnet-forge"

echo "===================================================="
echo " dotnet-forge Phase 2 — GitHub Setup"
echo " Repo: $REPO"
echo "===================================================="
echo ""

# ─────────────────────────────────────────────────────────────────────────────
# STEP 1: Create Milestones
# ─────────────────────────────────────────────────────────────────────────────
echo ">>> Creating Milestones..."

get_or_create_milestone() {
  local title="$1"
  local desc="$2"
  local num
  # Check if milestone already exists
  num=$(gh api "repos/$REPO/milestones" --jq ".[] | select(.title == \"$title\") | .number" 2>/dev/null || true)
  if [ -n "$num" ]; then
    echo "  Milestone already exists #$num: $title" >&2
  else
    num=$(gh api "repos/$REPO/milestones" \
      --method POST \
      -f title="$title" \
      -f description="$desc" \
      -f state="open" \
      --jq '.number')
    echo "  Created milestone #$num: $title" >&2
  fi
  # Return the title — gh issue create --milestone expects the name, not the number
  echo "$title"
}

M1=$(get_or_create_milestone \
  "Phase 2.1 — Core Abstractions & Entities" \
  "Define the IDeviceAdapter interface and SensorReading entity that everything else in Phase 2 depends on.")

M2=$(get_or_create_milestone \
  "Phase 2.2 — Device Adapters (Infrastructure)" \
  "Implement Modbus and OPC-UA protocol adapters, plus the AdapterFactory singleton that manages live adapter instances.")

M3=$(get_or_create_milestone \
  "Phase 2.3 — Data Persistence" \
  "Extend AppDbContext with SensorReadings, add an EF Core migration, and add a SensorReadingRepository for time-range queries.")

M4=$(get_or_create_milestone \
  "Phase 2.4 — Real-Time Streaming (SignalR)" \
  "Add the DeviceDataHub and wire SignalR into Program.cs so connected clients can subscribe to live tag updates.")

M5=$(get_or_create_milestone \
  "Phase 2.5 — Polling Background Service" \
  "Implement PollingBackgroundService that continuously reads all registered adapters, persists readings, and broadcasts to SignalR groups.")

M6=$(get_or_create_milestone \
  "Phase 2.6 — REST API Surface" \
  "Expose AdaptersController (register/list/remove adapters) and SensorReadingsController (query historical data) over HTTP.")

M7=$(get_or_create_milestone \
  "Phase 2.7 — Packages, DI Wiring & Config" \
  "Install NuGet packages, update ServiceCollectionExtensions to register all Phase 2 services, and extend docker-compose for local simulator support.")

M8=$(get_or_create_milestone \
  "Phase 2.8 — Testing & Documentation" \
  "Add integration tests for the adapter registration and polling flow, and update the README with Phase 2 usage instructions.")

echo ""

# ─────────────────────────────────────────────────────────────────────────────
# STEP 2: Create GitHub Labels (reusable tags for issues)
# ─────────────────────────────────────────────────────────────────────────────
echo ">>> Creating Labels..."

create_label_safe() {
  local name="$1" color="$2" desc="$3"
  gh api "repos/$REPO/labels" --method POST \
    -f name="$name" -f color="$color" -f description="$desc" \
    --silent 2>/dev/null || echo "  (label '$name' may already exist — skipping)"
  echo "  Label: $name"
}

create_label_safe "phase-2"      "0075ca" "Phase 2 — Manufacturing Hooks"
create_label_safe "core"         "e4e669" "Changes in src/Core"
create_label_safe "infrastructure" "c5def5" "Changes in src/Infrastructure"
create_label_safe "api"          "bfd4f2" "Changes in src/Api"
create_label_safe "signalr"      "d93f0b" "SignalR real-time streaming"
create_label_safe "modbus"       "fbca04" "Modbus protocol adapter"
create_label_safe "opc-ua"       "0e8a16" "OPC-UA protocol adapter"
create_label_safe "ef-migration" "f9d0c4" "EF Core database migration"
create_label_safe "testing"      "cc317c" "Tests and test infrastructure"
create_label_safe "docs"         "0052cc" "Documentation updates"
create_label_safe "enhancement"  "a2eeef" "New feature or improvement"

echo ""

# ─────────────────────────────────────────────────────────────────────────────
# STEP 3: Create Issues
# ─────────────────────────────────────────────────────────────────────────────
echo ">>> Creating Issues..."

create_issue() {
  local title="$1" body="$2" milestone="$3" labels="$4"
  local url num label_flags=() existing
  # Skip if an open issue with this exact title already exists
  existing=$(gh issue list --repo "$REPO" --state open --search "\"$title\" in:title" --json number,title \
    --jq ".[] | select(.title == \"$title\") | .number" 2>/dev/null | head -1 || true)
  if [ -n "$existing" ]; then
    echo "  Issue already exists #$existing: $title"
    return
  fi
  # Build one --label flag per label (gh CLI requires separate flags, not CSV)
  IFS=',' read -ra label_arr <<< "$labels"
  for lbl in "${label_arr[@]}"; do
    label_flags+=(--label "$lbl")
  done
  url=$(gh issue create \
    --repo "$REPO" \
    --title "$title" \
    --body "$body" \
    --milestone "$milestone" \
    "${label_flags[@]}")
  num=$(echo "$url" | grep -oE '[0-9]+$')
  echo "  Created issue #$num: $title"
}

# ── Milestone 1: Core Abstractions ──────────────────────────────────────────

create_issue \
  "Add IDeviceAdapter interface with AdapterConfig and TagValue records" \
'## Context
Phase 2 requires a protocol-agnostic abstraction for device communication. All adapters (Modbus, OPC-UA, future protocols) implement this interface; the Api layer only ever depends on the interface.

## File to create
`src/Core/Interfaces/IDeviceAdapter.cs`

## Acceptance Criteria
- [ ] `IDeviceAdapter` interface defined with `AdapterId`, `IsConnected`, `ConnectAsync`, `ReadTagAsync`, `WriteTagAsync`, `SubscribeAsync`, `DisconnectAsync`, and `DisposeAsync`
- [ ] `AdapterConfig` record with `Host`, `Port`, `Protocol`, `PollInterval`, and `Options` fields
- [ ] `TagValue` record with `AdapterId`, `TagAddress`, `Value`, `Timestamp`, and `Unit` fields
- [ ] `IDeviceAdapter` extends `IAsyncDisposable`
- [ ] Namespace: `Core.Interfaces`
- [ ] No external package dependencies (Core has none)' \
  "$M1" "phase-2,core,enhancement"

create_issue \
  "Add SensorReading entity to Core" \
'## Context
Persist every tag read from a device. Stored as a string value (cast on read) to accommodate the heterogeneous output of different register types.

## File to create
`src/Core/Entities/SensorReading.cs`

## Acceptance Criteria
- [ ] `SensorReading` inherits `BaseEntity` (gets `Id`, `CreatedAt`, `UpdatedAt`)
- [ ] Properties: `AdapterId` (string), `TagAddress` (string), `Value` (string), `Unit` (string), `RecordedAt` (DateTime, UTC)
- [ ] Namespace: `Core.Entities`' \
  "$M1" "phase-2,core,enhancement"

# ── Milestone 2: Device Adapters ─────────────────────────────────────────────

create_issue \
  "Implement ModbusAdapter" \
'## Context
TCP Modbus client wrapping the NModbus library. Supports HR (holding registers), CO (coils), DI (discrete inputs), IR (input registers).

## File to create
`src/Infrastructure/Adapters/ModbusAdapter.cs`

## Tag address format
`"HR:0:1"` = register type, start address, count

## Acceptance Criteria
- [ ] Implements `IDeviceAdapter`
- [ ] `ConnectAsync` opens a `TcpClient` and creates an `IModbusMaster` via `ModbusIpMaster.CreateIp`
- [ ] `ReadTagAsync` parses tag address and dispatches to the correct `_master.Read*Async` overload
- [ ] `WriteTagAsync` supports HR (single register) and CO (single coil); throws `NotSupportedException` for unsupported types
- [ ] `SubscribeAsync` loops `ReadTagAsync` → `yield return` → `Task.Delay(PollInterval)` until cancellation
- [ ] `DisconnectAsync` / `DisposeAsync` dispose `_master` and close `_client`
- [ ] `IsConnected` reflects `_client?.Connected`
- [ ] `AdapterId` set to `"modbus-{host}:{port}"` on connect
- [ ] NModbus package installed: `dotnet add src/Infrastructure package NModbus`' \
  "$M2" "phase-2,infrastructure,modbus,enhancement"

create_issue \
  "Implement OpcUaAdapter" \
'## Context
OPC-UA client wrapping OPCFoundation.NetStandard.Opc.Ua. Connects to an OPC-UA endpoint, reads/writes/subscribes to node IDs.

## File to create
`src/Infrastructure/Adapters/OpcUaAdapter.cs`

## Tag address format
OPC-UA node ID string, e.g. `"ns=2;s=Temperature"`

## Acceptance Criteria
- [ ] Implements `IDeviceAdapter`
- [ ] `ConnectAsync` creates and connects an `OpcUaClient` (or equivalent SDK session) to `opc.tcp://{Host}:{Port}`
- [ ] `ReadTagAsync` reads a single node by `NodeId` and wraps the result in `TagValue`
- [ ] `WriteTagAsync` writes a `DataValue` to the node
- [ ] `SubscribeAsync` loops poll-based reads (SDK subscriptions may be added as an enhancement)
- [ ] `DisconnectAsync` / `DisposeAsync` disconnect and dispose the session
- [ ] `AdapterId` set to `"opcua-{host}:{port}"` on connect
- [ ] OPC Foundation package installed: `dotnet add src/Infrastructure package OPCFoundation.NetStandard.Opc.Ua`' \
  "$M2" "phase-2,infrastructure,opc-ua,enhancement"

create_issue \
  "Implement AdapterFactory" \
'## Context
DI-registered singleton. Maintains a `ConcurrentDictionary` of live adapter instances keyed by `AdapterId`. `PollingBackgroundService` iterates `GetAll()` each cycle.

## File to create
`src/Infrastructure/Adapters/AdapterFactory.cs`

## Acceptance Criteria
- [ ] Registered as a singleton in DI
- [ ] `RegisterAsync(AdapterConfig)` creates the correct adapter via a `Protocol` switch, calls `ConnectAsync`, and stores in the dictionary
- [ ] `GetAll()` returns all stored adapter values
- [ ] `Get(adapterId)` returns a single adapter or `null`
- [ ] `RemoveAsync(adapterId)` calls `DisconnectAsync`, `DisposeAsync`, and removes from the dictionary
- [ ] Thread-safe (uses `ConcurrentDictionary`)' \
  "$M2" "phase-2,infrastructure,enhancement"

# ── Milestone 3: Data Persistence ────────────────────────────────────────────

create_issue \
  "Update AppDbContext with SensorReadings DbSet" \
'## Context
`AppDbContext` currently only has `Widgets`. Phase 2 needs a `SensorReadings` table.

## File to edit
`src/Infrastructure/Data/AppDbContext.cs`

## Acceptance Criteria
- [ ] `public DbSet<SensorReading> SensorReadings => Set<SensorReading>();` added
- [ ] Remove the `// Phase 2:` comment placeholder
- [ ] `OnModelCreating` remains unchanged (uses `ApplyConfigurationsFromAssembly` auto-discovery)' \
  "$M3" "phase-2,infrastructure,ef-migration,enhancement"

create_issue \
  "Add EF Core migration for SensorReading" \
'## Context
Generate and apply an EF Core migration to create the `SensorReadings` table.

## Commands
```bash
dotnet ef migrations add AddSensorReading -p src/Infrastructure -s src/Api
dotnet ef database update -p src/Infrastructure -s src/Api
```

## Acceptance Criteria
- [ ] Migration file created under `src/Infrastructure/Migrations/`
- [ ] Migration creates a `SensorReadings` table with all `SensorReading` columns plus the `BaseEntity` columns (`Id`, `CreatedAt`, `UpdatedAt`)
- [ ] `RecordedAt` column is indexed (add an `IEntityTypeConfiguration<SensorReading>` under `Infrastructure/Data/` to configure the index)
- [ ] Migration is reviewed and committed' \
  "$M3" "phase-2,infrastructure,ef-migration,enhancement"

create_issue \
  "Add SensorReadingRepository with time-range and adapter queries" \
'## Context
`GenericRepository<SensorReading>` covers basic CRUD, but callers will need filtered queries (by adapter, tag, and time range) that are awkward to express as LINQ predicates from outside the layer.

## File to create
`src/Infrastructure/Data/Repositories/SensorReadingRepository.cs`

## Acceptance Criteria
- [ ] Extends `GenericRepository<SensorReading>`
- [ ] `GetByAdapterAsync(string adapterId, CancellationToken)` — returns all readings for an adapter
- [ ] `GetByTagAsync(string adapterId, string tagAddress, DateTime from, DateTime to, CancellationToken)` — filtered + ordered by `RecordedAt`
- [ ] `GetLatestAsync(string adapterId, string tagAddress, CancellationToken)` — single most recent reading
- [ ] Registered in DI in `ServiceCollectionExtensions`' \
  "$M3" "phase-2,infrastructure,enhancement"

# ── Milestone 4: SignalR ──────────────────────────────────────────────────────

create_issue \
  "Add DeviceDataHub (SignalR)" \
'## Context
SignalR hub that lets authenticated WebSocket clients subscribe/unsubscribe to live tag streams. Groups are keyed as `"{adapterId}::{tagAddress}"`. The polling service broadcasts into these groups.

## File to create
`src/Api/Hubs/DeviceDataHub.cs`

## Acceptance Criteria
- [ ] Extends `Hub`
- [ ] Decorated with `[Authorize]` — only authenticated clients may connect
- [ ] `SubscribeToTag(adapterId, tagAddress)` adds the connection to the group
- [ ] `UnsubscribeFromTag(adapterId, tagAddress)` removes the connection from the group
- [ ] Group key helper: `$"{adapterId}::{tagAddress}"`' \
  "$M4" "phase-2,api,signalr,enhancement"

create_issue \
  "Wire SignalR into Program.cs and ServiceCollectionExtensions" \
'## Context
`DeviceDataHub` needs to be registered and mapped in the ASP.NET Core pipeline.

## Files to edit
- `src/Api/Extensions/ServiceCollectionExtensions.cs`
- `src/Api/Program.cs`

## Acceptance Criteria
- [ ] `AddSignalR()` called in `AddInfrastructure` or a new `AddSignalRServices()` extension method
- [ ] `app.MapHub<DeviceDataHub>("/hubs/device-data")` added in `Program.cs`
- [ ] `Microsoft.AspNetCore.SignalR` package added to `src/Api`
- [ ] Hub endpoint included in Swagger documentation (or noted as WebSocket-only)' \
  "$M4" "phase-2,api,signalr,enhancement"

# ── Milestone 5: Polling Background Service ──────────────────────────────────

create_issue \
  "Implement PollingBackgroundService" \
'## Context
Runs as a hosted `BackgroundService`. Each iteration loops all registered adapters, reads their configured tags, persists to `SensorReadings`, and broadcasts the `TagValue` to the matching SignalR group.

## File to create
`src/Infrastructure/Services/PollingBackgroundService.cs`

## Acceptance Criteria
- [ ] Extends `BackgroundService`
- [ ] Injects `IServiceScopeFactory`, `IHubContext<DeviceDataHub>`, `AdapterFactory`, `ILogger`
- [ ] `ExecuteAsync` loops while not cancelled; iterates `_adapterFactory.GetAll()`; skips disconnected adapters
- [ ] Tag list sourced from `AdapterConfig.Options["tags"]` (comma-separated), resolved via `AdapterFactory`
- [ ] For each tag read: persists `SensorReading` via a scoped `IRepository<SensorReading>`, then calls `_hub.Clients.Group(...).SendAsync("TagUpdate", reading, ct)`
- [ ] Errors on individual tags are caught and logged without stopping the loop
- [ ] `Task.Delay` between full cycles (configurable, default 1 s)' \
  "$M5" "phase-2,infrastructure,signalr,enhancement"

create_issue \
  "Register PollingBackgroundService in DI" \
'## Context
The polling service must be registered as a hosted service so ASP.NET Core starts it automatically.

## File to edit
`src/Api/Extensions/ServiceCollectionExtensions.cs`

## Acceptance Criteria
- [ ] `services.AddHostedService<PollingBackgroundService>()` added in the infrastructure wiring method
- [ ] `AdapterFactory` registered as a singleton
- [ ] Service starts cleanly on `docker compose up` with no registered adapters (empty loop is fine)' \
  "$M5" "phase-2,api,infrastructure,enhancement"

# ── Milestone 6: REST API Surface ────────────────────────────────────────────

create_issue \
  "Implement AdaptersController" \
'## Context
HTTP surface to dynamically register, list, and remove device adapters at runtime without restarting the API.

## File to create
`src/Api/Controllers/AdaptersController.cs`

## Endpoints
| Method | Route | Description |
|--------|-------|-------------|
| POST | /api/adapters | Register a new adapter and connect to the device |
| GET | /api/adapters | List all registered adapters and their connection status |
| DELETE | /api/adapters/{adapterId} | Disconnect and remove an adapter |

## Request body (POST)
```json
{
  "host": "192.168.1.50",
  "port": 502,
  "protocol": "modbus",
  "pollIntervalSeconds": 1,
  "tags": ["HR:0", "HR:1", "CO:5"]
}
```

## Acceptance Criteria
- [ ] Extends `BaseApiController` (uses `OkResult<T>`, `FromResult<T>` envelope)
- [ ] POST maps body to `AdapterConfig` and calls `AdapterFactory.RegisterAsync`
- [ ] GET returns a DTO list with `AdapterId`, `Protocol`, `Host`, `Port`, `IsConnected`
- [ ] DELETE calls `AdapterFactory.RemoveAsync` and returns 204
- [ ] All endpoints require `[Authorize]`
- [ ] Routes are `[Authorize]` protected' \
  "$M6" "phase-2,api,enhancement"

create_issue \
  "Implement SensorReadingsController" \
'## Context
HTTP surface to query historical sensor data stored in Postgres.

## File to create
`src/Api/Controllers/SensorReadingsController.cs`

## Endpoints
| Method | Route | Description |
|--------|-------|-------------|
| GET | /api/sensor-readings | List readings with optional filters |
| GET | /api/sensor-readings/latest | Latest reading per tag for a given adapter |

## Query parameters (GET /api/sensor-readings)
- `adapterId` (required)
- `tagAddress` (optional)
- `from` (optional, ISO 8601)
- `to` (optional, ISO 8601)
- `page` / `pageSize` (default 1 / 50)

## Acceptance Criteria
- [ ] Extends `BaseApiController`
- [ ] Uses `SensorReadingRepository` (not `GenericRepository` directly)
- [ ] Returns paginated `ApiResponse<IEnumerable<SensorReadingDto>>`
- [ ] All endpoints require `[Authorize]`' \
  "$M6" "phase-2,api,enhancement"

# ── Milestone 7: Packages, DI Wiring & Config ────────────────────────────────

create_issue \
  "Install Phase 2 NuGet packages" \
'## Context
Three new packages are needed for Phase 2.

## Commands
```bash
dotnet add src/Infrastructure package OPCFoundation.NetStandard.Opc.Ua
dotnet add src/Infrastructure package NModbus
dotnet add src/Api package Microsoft.AspNetCore.SignalR
```

## Acceptance Criteria
- [ ] All three packages added to their respective `.csproj` files
- [ ] `dotnet restore` completes without errors
- [ ] Package versions checked against GitHub Advisory Database for known vulnerabilities before adding' \
  "$M7" "phase-2,infrastructure,api,enhancement"

create_issue \
  "Update ServiceCollectionExtensions for Phase 2 services" \
'## Context
All Phase 2 services need to be wired into DI cleanly through the existing extension method pattern.

## File to edit
`src/Api/Extensions/ServiceCollectionExtensions.cs`

## Acceptance Criteria
- [ ] `AdapterFactory` registered as singleton
- [ ] `SensorReadingRepository` registered (scoped, via `IRepository<SensorReading>` or its own interface)
- [ ] `PollingBackgroundService` registered as a hosted service
- [ ] SignalR registered
- [ ] No service registration logic in `Program.cs` (keep it clean)' \
  "$M7" "phase-2,api,infrastructure,enhancement"

create_issue \
  "Update docker-compose and .env.example for Phase 2 dev tooling" \
'## Context
Developers need a way to run a Modbus simulator locally without physical hardware to test Phase 2 end-to-end.

## Files to edit
- `docker-compose.override.yml`
- `.env.example`

## Acceptance Criteria
- [ ] A `modbus-sim` service added to `docker-compose.override.yml` under the `dev` profile using a freely available Modbus TCP simulator image (e.g., `oitc/modbus-server` or similar)
- [ ] `MODBUS_SIM_PORT` variable added to `.env.example` with a default of `502`
- [ ] README section updated with how to point the POST /api/adapters call at the simulator
- [ ] Optional: OPC-UA simulator service (e.g., `mcr.microsoft.com/iotedge/opc-plc`) also added under `dev` profile' \
  "$M7" "phase-2,docs,enhancement"

# ── Milestone 8: Testing & Documentation ─────────────────────────────────────

create_issue \
  "Add integration tests for adapter registration and polling flow" \
'## Context
Phase 2 adds significant async complexity (background service + SignalR + EF). Integration tests verify the end-to-end flow without needing physical hardware.

## Suggested approach
- Use a Modbus TCP mock (in-process loopback server or a mock `IDeviceAdapter`) rather than real hardware
- Use `WebApplicationFactory<Program>` for in-process API testing
- Use `TestServer` + SignalR client to verify hub messages

## Acceptance Criteria
- [ ] Test project (or existing test project) can boot the API with an in-memory or test Postgres DB
- [ ] Test: POST /api/adapters with a mock adapter config succeeds and adapter appears in GET /api/adapters
- [ ] Test: After adapter registration, `PollingBackgroundService` produces at least one `SensorReading` row within the poll interval
- [ ] Test: A SignalR test client subscribed to the registered tag receives a `TagUpdate` message
- [ ] All tests pass in CI (`dotnet test`)' \
  "$M8" "phase-2,testing,enhancement"

create_issue \
  "Update README with Phase 2 usage instructions" \
'## Context
The README currently covers Phase 1 (clone → configure → docker compose up). Phase 2 adds device adapters, SignalR, and a background polling service that need to be documented.

## File to edit
`README.md`

## Acceptance Criteria
- [ ] New "Phase 2 — Manufacturing Hooks" section added after the Phase 1 section
- [ ] Instructions for running the Modbus simulator via `docker compose --profile dev up`
- [ ] Example POST /api/adapters request body
- [ ] SignalR connection example (JavaScript snippet or curl-equivalent)
- [ ] Note on tag address format for Modbus (`"HR:0:1"`) and OPC-UA (`"ns=2;s=Temperature"`)
- [ ] Table of all new Phase 2 endpoints' \
  "$M8" "phase-2,docs,enhancement"

echo ""

# ─────────────────────────────────────────────────────────────────────────────
# STEP 4: Create GitHub Project
# ─────────────────────────────────────────────────────────────────────────────
echo ">>> Creating GitHub Project..."

# Classic projects (v1) are deprecated — use Projects v2 via GraphQL
echo "  Creating Projects v2 via GraphQL..."

create_project_v2() {
  local owner_id project_url
  owner_id=$(gh api graphql -f query='
    query { repositoryOwner(login: "southwestmogrown") { id } }
  ' --jq '.data.repositoryOwner.id' 2>/dev/null) || return 1

  project_url=$(gh api graphql -f query="
    mutation {
      createProjectV2(input: {
        ownerId: \"$owner_id\"
        title: \"Phase 2 — Manufacturing Hooks\"
      }) {
        projectV2 { id url }
      }
    }
  " --jq '.data.createProjectV2.projectV2.url' 2>/dev/null) || return 1

  if [[ "$project_url" == https* ]]; then
    echo "  Created project v2: $project_url"
  else
    return 1
  fi
}

create_project_v2 || echo "  ⚠️  Project creation skipped — token lacks 'project' write scope. Create it manually at: https://github.com/users/southwestmogrown/projects/new"

# ─────────────────────────────────────────────────────────────────────────────
echo ""
echo "===================================================="
echo " ✅  Phase 2 setup complete!"
echo "     Milestones: 8"
echo "     Issues:     17"
echo "     Labels:     11"
echo "     Project:    Phase 2 — Manufacturing Hooks"
echo ""
echo "  Next: gh issue list --repo $REPO --label phase-2"
echo "===================================================="
