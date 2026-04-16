# JWT Bearer, Swagger, and Health Check Package Research for .NET 8

**Researched:** 2026-04-16  
**Target framework:** .NET 8 (`net8.0`) with `Microsoft.NET.Sdk.Web`

---

## 1. `Microsoft.AspNetCore.Authentication.JwtBearer`

### Verdict: Explicit `dotnet add package` required

Despite appearing on the NuGet "Used By" list for `Microsoft.AspNetCore.App`, the JwtBearer package is **not** part of the ASP.NET Core shared framework for .NET 8. Evidence:

- The package's own `.csproj` in the aspnetcore repo does **not** set `IsAspNetCoreApp = true`, which is the flag that marks a library as a shared-framework component.
- The official Microsoft Learn doc ([Configure JWT bearer authentication in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-8.0)) explicitly references it as a NuGet package: _"The Microsoft.AspNetCore.Authentication.JwtBearer NuGet package can be used to validate the JWT bearer tokens."_
- Every .NET 8 tutorial and official sample performs an explicit package add.

### Current stable version for .NET 8

**`8.0.26`** (released 2026-04-14 as part of the April 2026 .NET 8 patch)

```bash
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.26
```

> Note: The latest overall release is `10.0.x` (for .NET 10). Pin to `8.0.x` for a .NET 8 project to avoid pulling a cross-major version.

### Key dependency pulled in transitively

- `Microsoft.IdentityModel.Protocols.OpenIdConnect >= 7.1.2`  
  This brings in `Microsoft.IdentityModel.Tokens`, which supplies `TokenValidationParameters`.

---

## 2. `TokenValidationParameters.ClockSkew = TimeSpan.Zero`

### Verdict: Works exactly as expected; property is unchanged

`ClockSkew` is a `TimeSpan` property on `Microsoft.IdentityModel.Tokens.TokenValidationParameters`:

- **Assembly:** `Microsoft.IdentityModel.Tokens.dll`  
- **Current package version pulled in with JwtBearer 8.0.26:** `Microsoft.IdentityModel.Tokens 8.x` (the latest in that series is `8.16.0` at time of research)
- **Default value:** 300 seconds (5 minutes) — tokens are considered valid for up to 5 minutes after their `exp` claim
- **Setting to `TimeSpan.Zero`:** Valid and documented. The setter throws `ArgumentOutOfRangeException` only if the value is **less than** zero, so `TimeSpan.Zero` is a valid strict expiration.

**Official API reference:** [TokenValidationParameters.ClockSkew Property](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.tokens.tokenvalidationparameters.clockskew?view=msal-web-dotnet-latest)

**No changes** from .NET 6/7 behaviour. The property, type, and semantics are identical.

---

## 3. `Swashbuckle.AspNetCore`

### Verdict: Must be added explicitly; .NET 8 still uses Swashbuckle

Swashbuckle was removed from the ASP.NET Core Web API project template in **.NET 9** (see [GitHub announcement #54599](https://github.com/dotnet/aspnetcore/issues/54599)). In .NET 9+, Microsoft ships `Microsoft.AspNetCore.OpenApi` (built-in) instead.

**.NET 8 Web API projects still use Swashbuckle** — it is referenced by the .NET 8 template by default. For a new .NET 8 project, you must add it explicitly:

```bash
dotnet add package Swashbuckle.AspNetCore --version 6.9.0
```

### Version recommendation for .NET 8

| Version series | Status | Notes |
|---|---|---|
| **6.9.0** | Recommended for .NET 8 | The version the official Microsoft docs pin for .NET 8 (`dotnet add ... -v 6.6.2` in older docs; 6.9.0 released Oct 2024). Targets .NET Standard 2.0. Stable and widely used. |
| 8.x / 9.x | Also compatible | Drop older ASP.NET Core support; minor dependency bumps. No significant API changes for the common JWT security pattern. |
| **10.x (10.1.7)** | **Breaking changes** | Upgraded `Microsoft.OpenApi` to 2.x. `AddSecurityRequirement` signature changed — `OpenApiSecurityScheme.Reference` no longer exists. Old bearer security configuration code **does not compile** without updates. |

**Use `6.9.0` for .NET 8** unless you specifically need 10.x features and are willing to update the security definition code.

### `AddSecurityDefinition` / `AddSecurityRequirement` API — .NET 8 (6.9.0)

The API is **unchanged** from .NET 6/7 in Swashbuckle 6.x. The standard JWT bearer setup remains:

```csharp
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter a valid JWT token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
```

This pattern compiles and works correctly with Swashbuckle 6.9.0 targeting .NET 8. It breaks on Swashbuckle 10.x due to the `Microsoft.OpenApi` 2.x breaking changes (`Reference` removed from `OpenApiSecurityScheme`).

---

## 4. `Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore`

### Verdict: Explicit `dotnet add package` required — not transitive

This package is **not** included in the ASP.NET Core shared framework and is **not** automatically transitively included by EF Core packages (e.g., `Npgsql.EntityFrameworkCore.PostgreSQL`). It must be added explicitly.

Its sole dependency is `Microsoft.EntityFrameworkCore.Relational >= 8.0.x`, which means EF Core itself does not pull this package in the reverse direction.

### Current stable version for .NET 8

**`8.0.26`** (released 2026-04-14)

```bash
dotnet add package Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore --version 8.0.26
```

This package provides the `UseDeveloperExceptionPage` database error page middleware (`.UseMigrationsEndPoint()`) useful during development to surface EF Core migration errors in the browser.

---

## 5. Health Checks — `AddNpgsql()` for PostgreSQL

### Package name

The correct package is:

**`AspNetCore.HealthChecks.NpgSql`** (note capital S in `NpgSql`, no `EntityFrameworkCore` in the name)

- Published by: **Xabaril** (community project — [AspNetCore.Diagnostics.HealthChecks](https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks))
- This is **not** `AspNetCore.HealthChecks.NpgsqlEntityFrameworkCore` (a different, separate package)

### Current stable version

**`9.0.0`** (released 2024-12-19)

```bash
dotnet add package AspNetCore.HealthChecks.NpgSql --version 9.0.0
```

> The version number `9.0.0` refers to the package's own versioning scheme aligned with ASP.NET Core 9; it targets `.NET 8.0` and `.NET Standard 2.0` and is fully compatible with .NET 8 projects. For a strictly .NET 8-aligned package, `8.0.2` is the last 8.x release (2024-08-29). Either works; `9.0.0` is the latest stable and recommended.

### Extension method name

The method is **`AddNpgSql()`** (camelCase `S`, not `AddNpgsql`):

```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql("Host=...;Database=...;Username=...;Password=...");
```

Or with `NpgsqlDataSource` (recommended pattern from Npgsql 7+):

```csharp
// Register NpgsqlDataSource first (e.g., via Npgsql.DependencyInjection)
builder.Services.AddNpgsqlDataSource(connectionString);

// Health check resolves NpgsqlDataSource from DI automatically
builder.Services.AddHealthChecks()
    .AddNpgSql();
```

### Npgsql version compatibility

Package `9.0.0` declares dependency: **`Npgsql >= 8.0.3`**

It is designed for and fully works with **Npgsql 8.x**. It will not work with Npgsql 9.x out of the box — if you are on Npgsql 9, stay on `AspNetCore.HealthChecks.NpgSql 9.0.0` (which is compatible per its `>= 8.0.3` lower bound; NuGet will resolve to the installed version).

---

## 6. `app.MapHealthChecks("/health")`

### Verdict: Built into ASP.NET Core 8 — no additional package required

`MapHealthChecks` is part of the `Microsoft.AspNetCore.Diagnostics.HealthChecks` assembly, which **is** included in the `Microsoft.AspNetCore.App` shared framework. The [official Microsoft documentation](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-8.0) explicitly states:

> _"The Microsoft.AspNetCore.Diagnostics.HealthChecks package is referenced implicitly for ASP.NET Core apps."_

No `dotnet add package` is needed. Usage:

```csharp
builder.Services.AddHealthChecks(); // registers core health check services

var app = builder.Build();

app.MapHealthChecks("/health"); // built-in — no extra package
```

This is **unchanged** from .NET 6/7.

> **Do not confuse** this with `Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore` (item 4 above), which is a separate, explicitly-required package.

---

## Summary Table

| Package | Explicit `dotnet add package`? | Recommended version (net8.0) |
|---|---|---|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | **Yes** | `8.0.26` |
| `TokenValidationParameters.ClockSkew` | n/a (comes with JwtBearer) | In `Microsoft.IdentityModel.Tokens 8.x` |
| `Swashbuckle.AspNetCore` | **Yes** | `6.9.0` (use ≤ 9.x to avoid 10.x breaking changes) |
| `Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore` | **Yes** | `8.0.26` |
| `AspNetCore.HealthChecks.NpgSql` | **Yes** | `9.0.0` (or `8.0.2` for strict 8.x alignment) |
| `app.MapHealthChecks(...)` (built-in) | **No** | Ships with `Microsoft.AspNetCore.App` |

---

## .NET 9 Differences to Be Aware Of (for future migration)

- **Swashbuckle removed from template.** .NET 9 uses `Microsoft.AspNetCore.OpenApi` (`AddOpenApi()` / `MapOpenApi()`) instead. Swashbuckle 10.x is still installable but requires updated security configuration code.
- **`AddSecurityRequirement` API changed** in Swashbuckle 10.x (Microsoft.OpenApi 2.x): `OpenApiSecurityScheme.Reference` was removed. The old `new OpenApiSecurityRequirement { { new OpenApiSecurityScheme { Reference = ... } } }` pattern does not compile.
- The `MapHealthChecks`, `AddHealthChecks`, JwtBearer, and EF Core diagnostics packages remain the same explicit-add pattern in .NET 9+.

---

## Sources

- [NuGet: Microsoft.AspNetCore.Authentication.JwtBearer](https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.JwtBearer)
- [MS Docs: Configure JWT bearer authentication in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-8.0)
- [MS Docs: TokenValidationParameters.ClockSkew Property](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.tokens.tokenvalidationparameters.clockskew?view=msal-web-dotnet-latest)
- [NuGet: Swashbuckle.AspNetCore](https://www.nuget.org/packages/Swashbuckle.AspNetCore)
- [NuGet: Swashbuckle.AspNetCore 6.9.0](https://www.nuget.org/packages/Swashbuckle.AspNetCore/6.9.0)
- [MS Docs: Get started with Swashbuckle and ASP.NET Core (.NET 8)](https://learn.microsoft.com/en-us/aspnet/core/tutorials/getting-started-with-swashbuckle?view=aspnetcore-8.0)
- [GitHub: Swashbuckle removed in .NET 9 announcement #54599](https://github.com/dotnet/aspnetcore/issues/54599)
- [NuGet: Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore](https://www.nuget.org/packages/Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore)
- [NuGet: AspNetCore.HealthChecks.NpgSql](https://www.nuget.org/packages/AspNetCore.HealthChecks.NpgSql/)
- [GitHub: Xabaril/AspNetCore.Diagnostics.HealthChecks — NpgSql README](https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks/blob/master/src/HealthChecks.NpgSql/README.md)
- [MS Docs: Health checks in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-8.0)
- [GitHub: Microsoft.AspNetCore.Authentication.JwtBearer.csproj (main branch)](https://github.com/dotnet/aspnetcore/blob/main/src/Security/Authentication/JwtBearer/src/Microsoft.AspNetCore.Authentication.JwtBearer.csproj)
