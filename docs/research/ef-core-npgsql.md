# EF Core 8 + Npgsql Compatibility Research

> Researched: 2026-04-16. All version numbers verified against NuGet.org and official docs.

---

## 1. Package Version Triplet (EF Core 8 + Npgsql)

### Latest stable 8.x releases (as of 2026-04-16)

| Package | Latest 8.x Version | Release Date |
|---|---|---|
| `Microsoft.EntityFrameworkCore` | **8.0.26** | 2026-04-14 |
| `Microsoft.EntityFrameworkCore.Design` | **8.0.26** | 2026-04-14 |
| `Microsoft.EntityFrameworkCore.Tools` | **8.0.26** | 2026-04-14 |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | **8.0.11** | 2024-11-18 |

> **Note on version mismatch**: The Microsoft EF Core packages have continued releasing security/maintenance patches through the .NET 8 LTS support window, reaching 8.0.26. The Npgsql EF Core provider for the 8.x line stopped at **8.0.11** — Npgsql moved to a 9.x provider to track EF Core 9. This is safe: `Npgsql.EntityFrameworkCore.PostgreSQL 8.0.11` declares a dependency of `Microsoft.EntityFrameworkCore >= 8.0.11`, so it is compatible with any later 8.x patch release including 8.0.26. NuGet resolves this correctly.

### Verified dependency chain for `Npgsql.EntityFrameworkCore.PostgreSQL 8.0.11`

```
Npgsql.EntityFrameworkCore.PostgreSQL 8.0.11
  ├── Microsoft.EntityFrameworkCore             >= 8.0.11
  ├── Microsoft.EntityFrameworkCore.Abstractions >= 8.0.11
  ├── Microsoft.EntityFrameworkCore.Relational  >= 8.0.11
  └── Npgsql                                    >= 8.0.6
```

### Recommended `.csproj` package references

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.26" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.26">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
</PackageReference>
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.26">
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.11" />
```

---

## 2. `dotnet-ef` Global Tool

The `dotnet-ef` global tool version must match the major.minor of the EF Core packages in the project. For EF Core 8:

```bash
dotnet tool install --global dotnet-ef --version 8.0.26
```

- Latest 8.x version: **8.0.26** (released 2026-04-14, verified on NuGet.org)
- Requires .NET 8 SDK or later
- The tool version should be pinned to the same 8.x series as the project packages

To update an existing installation:

```bash
dotnet tool update --global dotnet-ef --version 8.0.26
```

---

## 3. `ApplyConfigurationsFromAssembly`

### Method signature (EF Core 8)

```csharp
[RequiresUnreferencedCode("This API isn't safe for trimming, since it searches for types in an arbitrary assembly.")]
public virtual ModelBuilder ApplyConfigurationsFromAssembly(
    Assembly assembly,
    Func<Type, bool>? predicate = default);
```

### Behavior

- Scans the provided assembly for all concrete, non-abstract types that implement `IEntityTypeConfiguration<TEntity>`.
- Instantiates each configuration class using its **public, parameterless constructor** and calls `Configure()` on it.
- An optional `predicate` parameter allows filtering which types are applied.
- Returns the same `ModelBuilder` instance for chaining.

### Compared to EF Core 7

No behavioral changes between EF7 and EF8. The method signature and discovery logic are identical.

> **EF9 note (flagged for awareness)**: EF Core 9 extended this method to also support non-public constructors (e.g. private nested configuration classes). This change is **not** present in EF8 — all `IEntityTypeConfiguration<T>` implementations must have a public, parameterless constructor when using EF8.

### Typical usage

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(MyDbContext).Assembly);
}
```

---

## 4. `SaveChangesAsync` Override

### Valid override signature in EF Core 8

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    // pre-save logic using ChangeTracker.Entries<T>()
    return await base.SaveChangesAsync(cancellationToken);
}
```

This signature is **valid and unchanged** from EF7. The base class declares:

```csharp
public virtual Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
```

There is also a second overload:

```csharp
public virtual Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default);
```

### `ChangeTracker.Entries<T>()`

The generic overload signature in EF8:

```csharp
public virtual IEnumerable<EntityEntry<TEntity>> Entries<TEntity>() where TEntity : class;
```

This is unchanged from EF7. It returns only tracked entries of (or assignable to) the specified type, and can be combined with `.State` checks for audit patterns:

```csharp
var entries = ChangeTracker.Entries<IAuditableEntity>()
    .Where(e => e.State is EntityState.Added or EntityState.Modified);
```

### EF8 breaking change — `ITypeBase` replaces `IEntityType` in some APIs

If you use low-level model-building APIs (`IProperty.DeclaringEntityType`, `IEntityTypeIgnoredConvention`, `IValueGeneratorSelector.Select`), these now accept `ITypeBase` instead of `IEntityType` due to the introduction of complex types. The `ChangeTracker.Entries<T>()` public API is **not** affected.

---

## 5. `FindAsync` with Cancellation Token

### The two overloads in EF Core 8

```csharp
// Overload 1 — no cancellation token (params, compiler-friendly)
ValueTask<TEntity?> FindAsync(params object?[]? keyValues);

// Overload 2 — with cancellation token (explicit array required)
ValueTask<TEntity?> FindAsync(object?[]? keyValues, CancellationToken cancellationToken);
```

> **Key difference from EF6**: In EF6, the token came *first* (`FindAsync(CancellationToken, params object[])`). In EF Core, the token comes *last* and the key values array is **not** `params` in the cancellation token overload.

### Correct syntax in EF Core 8

```csharp
// Single primary key with cancellation token — key values MUST be wrapped in array
var entity = await dbSet.FindAsync(new object[] { id }, cancellationToken);

// Composite primary key with cancellation token
var entity = await dbSet.FindAsync(new object[] { key1, key2 }, cancellationToken);
```

### The common mistake (will compile but fail at runtime)

```csharp
// WRONG — compiler interprets (id, ct) as two key values via the params overload
var entity = await dbSet.FindAsync(id, cancellationToken);
// Runtime error: "Entity type is defined with a single key property, but 2 values were passed"
```

The reason: when `cancellationToken` is passed without an explicit array, the compiler resolves to `FindAsync(params object?[]?)`, treating both `id` and `cancellationToken` as elements of the key-values array.

### Correct form summary

```csharp
// Always wrap key values in an explicit array when passing a CancellationToken:
await context.Widgets.FindAsync(new object[] { id }, ct);
```

---

## 6. Postgres 16 + Npgsql 8 Driver Compatibility

**Npgsql 8 fully supports PostgreSQL 16.** The key facts:

- Npgsql's compatibility policy: supports all PostgreSQL versions within the current 5-year support window. PostgreSQL 16 was released in October 2023 and is well within that window.
- `Npgsql.EntityFrameworkCore.PostgreSQL 8.0.x` depends on `Npgsql >= 8.0.6` (the ADO.NET driver), which supports PostgreSQL 14 and later including 16.
- The Npgsql EF Core 8.0 provider sets the **default minimum assumed PostgreSQL version to 14** (raised from 12 in 7.x). PostgreSQL 16 exceeds this minimum with no issues.
- If you need to run against PostgreSQL older than 14, you must call `SetPostgresVersion()` in `UseNpgsql()`. For PostgreSQL 16, no special version configuration is needed.

**Verified**: Npgsql GitHub releases and the official 8.0 release notes confirm no known incompatibilities with PostgreSQL 16.

---

## 7. `UseNpgsql` Connection String Format for Docker Compose

### Keyword reference (from official Npgsql docs)

| Keyword | Purpose | Default |
|---|---|---|
| `Host` | Hostname or IP of the PostgreSQL server | `localhost` |
| `Port` | TCP port | `5432` |
| `Database` | Database name | (same as username) |
| `Username` | Login username | — |
| `Password` | Login password | — |

Connection string values are semicolon-separated key=value pairs and are case-insensitive.

### Correct format for Docker Compose where the DB service is named `db`

```
Host=db;Port=5432;Database=myapp;Username=postgres;Password=secret
```

- `Host=db` resolves to the Docker Compose service named `db` via Docker's internal DNS. This is the standard approach and works with Npgsql 8 without any special configuration.
- `Port` can be omitted if using the default 5432.

### Registration in `Program.cs`

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
```

### `appsettings.json` / environment variable

```json
{
  "ConnectionStrings": {
    "Default": "Host=db;Port=5432;Database=myapp;Username=postgres;Password=secret"
  }
}
```

---

## Summary of EF8 Changes Relevant to This Project

| Area | Status |
|---|---|
| `ApplyConfigurationsFromAssembly` | Unchanged from EF7. Public parameterless constructors required. |
| `SaveChangesAsync(CancellationToken)` override | Unchanged from EF7. Signature is valid. |
| `ChangeTracker.Entries<T>()` | Unchanged from EF7. |
| `FindAsync` with cancellation token | Same as EF7. Must use `new object[] { id }` syntax — not `(id, ct)`. |
| Enums in JSON columns | **Breaking change**: stored as `int` by default in EF8 (was `string` in EF7). Explicit `.HasConversion<string>()` required to keep string storage. |
| `IProperty.DeclaringEntityType` | Obsoleted in EF8; use `IProperty.DeclaringType` instead (low-impact, only affects custom conventions). |
| PostgreSQL 16 + Npgsql 8 | Fully supported. No special configuration needed. |

---

## Sources

- [NuGet: Microsoft.EntityFrameworkCore 8.0.26](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore/8.0.26)
- [NuGet: Microsoft.EntityFrameworkCore.Design 8.0.26](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Design/8.0.26)
- [NuGet: Microsoft.EntityFrameworkCore.Tools 8.0.26](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Tools/8.0.26)
- [NuGet: Npgsql.EntityFrameworkCore.PostgreSQL 8.0.11](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL/8.0.11)
- [NuGet: dotnet-ef 8.0.26](https://www.nuget.org/packages/dotnet-ef/8.0.26)
- [Npgsql EF Core 8.0 Release Notes](https://www.npgsql.org/efcore/release-notes/8.0.html)
- [Npgsql 8.0 Driver Release Notes](https://www.npgsql.org/doc/release-notes/8.0.html)
- [Npgsql Compatibility Notes](https://www.npgsql.org/doc/compatibility.html)
- [Npgsql Connection String Parameters](https://www.npgsql.org/doc/connection-string-parameters.html)
- [MS Docs: ModelBuilder.ApplyConfigurationsFromAssembly (EF Core 8)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.modelbuilder.applyconfigurationsfromassembly?view=efcore-8.0)
- [MS Docs: DbContext.SaveChangesAsync (EF Core 8)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.dbcontext.savechangesasync?view=efcore-8.0)
- [MS Docs: DbSet.FindAsync (EF Core 8)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.dbset-1.findasync?view=efcore-8.0)
- [MS Docs: Breaking changes in EF Core 8.0](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-8.0/breaking-changes)
- [EF Core tools reference (.NET CLI)](https://learn.microsoft.com/en-us/ef/core/cli/dotnet)
