# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**dotnet-forge** is a reusable ASP.NET Core 8 API template targeting startups and manufacturing clients. It is built in three phases:

- **Phase 1** — Core API scaffold: JWT auth, PostgreSQL via EF Core, Docker Compose, Swagger
- **Phase 2** — Manufacturing hooks: OPC-UA and Modbus device adapters, SignalR real-time streaming, sensor data persistence
- **Phase 3** — Client kit: threshold-based alert rules, multi-channel notifications (email, Slack, webhooks), `scripts/scaffold-client.sh` for per-client setup

The plan document `dotnet-forge-plan.md` contains the full architecture blueprint including all file contents.

## Bootstrap Commands

```bash
# One-time solution setup (Phase 1)
dotnet new sln -n dotnet-forge
dotnet new webapi -n Api -o src/Api --no-openapi
dotnet new classlib -n Core -o src/Core
dotnet new classlib -n Infrastructure -o src/Infrastructure
dotnet sln add src/Api src/Core src/Infrastructure
dotnet add src/Api reference src/Core src/Infrastructure
dotnet add src/Infrastructure reference src/Core

# EF Core CLI (global, one-time)
dotnet tool install --global dotnet-ef
```

## Build & Run

```bash
# Docker (primary dev workflow)
cp .env.example .env           # fill in secrets
docker compose up --build      # API + Postgres
docker compose --profile dev up  # adds pgAdmin at localhost:5050

# Local watch mode
dotnet watch run -p src/Api

# EF migrations
dotnet ef migrations add <Name> -p src/Infrastructure -s src/Api
dotnet ef database update -p src/Infrastructure -s src/Api
```

Endpoints after `docker compose up`:
- API: `localhost:5000`
- Swagger: `localhost:5000/swagger`
- Health: `localhost:5000/health`
- Postgres: `localhost:5432`

## Project Structure

```
src/
├── dotnet-forge.sln
├── Api/                          # ASP.NET Core Web API
│   ├── Controllers/              # BaseApiController (response envelope), AuthController, resource controllers
│   ├── Middleware/               # ExceptionMiddleware — maps Core.Exceptions → RFC 7807 Problem Details
│   ├── Hubs/                     # SignalR DeviceDataHub (Phase 2)
│   └── Extensions/               # ServiceCollectionExtensions — keeps Program.cs clean
├── Core/                         # Class library, no dependencies
│   ├── Entities/                 # BaseEntity (auto-timestamps), SensorReading, AlertRule
│   ├── Interfaces/               # IRepository<T>, IDeviceAdapter, INotificationChannel
│   ├── Models/                   # Result<T> (railway-oriented), ApiResponse<T>
│   └── Exceptions/               # NotFoundException, ValidationException
└── Infrastructure/               # Implements Core interfaces
    ├── Data/                     # AppDbContext, GenericRepository<T>
    ├── Services/                 # TokenService, PollingBackgroundService, AlertEvaluator, NotificationDispatcher
    └── Adapters/                 # ModbusAdapter, OpcUaAdapter, AdapterFactory (Phase 2)
```

## Architecture Patterns

**Layered dependency direction:** `Api → Core ← Infrastructure`. Api and Infrastructure both reference Core; neither references the other.

**Railway-oriented result:** Services return `Result<T>` instead of throwing across layer boundaries. Controllers call `FromResult<T>()` on `BaseApiController` to unwrap into HTTP responses.

**Generic repository:** `GenericRepository<T>` covers standard CRUD. Extend it for domain-specific queries: `class WidgetRepository : GenericRepository<Widget>`.

**Entity type configuration:** Use `IEntityTypeConfiguration<T>` implementations; `AppDbContext.OnModelCreating` calls `ApplyConfigurationsFromAssembly()` to auto-discover them — don't add config directly to `OnModelCreating`.

**Exception → HTTP mapping:** Throw `NotFoundException` or `ValidationException` anywhere in Infrastructure or Core. `ExceptionMiddleware` catches and maps them (404, 422). Stack traces only leak in Development.

**Device adapters (Phase 2):** Protocol implementations (`ModbusAdapter`, `OpcUaAdapter`) implement `IDeviceAdapter`. `AdapterFactory` manages live adapter instances. `PollingBackgroundService` iterates adapters, reads tags, persists to `SensorReadings`, and broadcasts via SignalR hub groups keyed as `"{adapterId}::{tagAddress}"`.

**Notification channels (Phase 3):** Implement `INotificationChannel` and register it in DI. `NotificationDispatcher` fans out to all matching channels. `AlertEvaluator` is called after each tag read and respects per-rule cooldowns.

## Key Conventions

- All timestamps use `DateTime.UtcNow` — never local time
- All IDs are GUIDs (`Guid.NewGuid()` on `BaseEntity`)
- `CancellationToken` is threaded through all async calls
- JWT uses HS256 with 8-hour expiration and `ClockSkew = TimeSpan.Zero` (no grace period)
- No ASP.NET Identity — plain JWT only
- Configuration accessed via `IConfiguration["Section:Key"]`; all service wiring goes in `ServiceCollectionExtensions`
- Modbus tag address format: `"HR:0:1"` = register type, start address, count

## NuGet Packages by Phase

```bash
# Phase 1
dotnet add src/Api package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add src/Api package Swashbuckle.AspNetCore
dotnet add src/Api package Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore
dotnet add src/Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/Infrastructure package Microsoft.EntityFrameworkCore.Design
dotnet add src/Infrastructure package Microsoft.EntityFrameworkCore.Tools

# Phase 2
dotnet add src/Infrastructure package OPCFoundation.NetStandard.Opc.Ua
dotnet add src/Infrastructure package NModbus
dotnet add src/Api package Microsoft.AspNetCore.SignalR

# Phase 3
dotnet add src/Infrastructure package MailKit
```

## Client Scaffolding

```bash
chmod +x scripts/scaffold-client.sh
./scripts/scaffold-client.sh
# Interactive: client slug, DB password, JWT secret, API port
# Output: cloned + renamed solution, generated .env, ready for docker compose up --build
```
