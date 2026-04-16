# dotnet-forge

**ASP.NET Core 8 · PostgreSQL · Docker · OPC-UA · Modbus**  
A reusable, deploy-anywhere API template targeting startups and manufacturing clients.

---

## Folder Structure

```
dotnet-forge/
├── Dockerfile
├── docker-compose.yml
├── docker-compose.override.yml     # dev: hot reload, pgAdmin profile
├── .env.example
├── .dockerignore
├── .gitignore
├── README.md
├── scripts/
│   ├── scaffold-client.sh          # Phase 3: interactive client setup
│   └── seed.sql
└── src/
    ├── dotnet-forge.sln
    ├── Api/
    │   ├── Api.csproj
    │   ├── Program.cs
    │   ├── Controllers/
    │   │   ├── BaseApiController.cs
    │   │   ├── AuthController.cs
    │   │   └── WidgetsController.cs    # placeholder CRUD resource
    │   ├── Middleware/
    │   │   └── ExceptionMiddleware.cs
    │   └── Extensions/
    │       └── ServiceCollectionExtensions.cs
    ├── Core/
    │   ├── Core.csproj
    │   ├── Entities/
    │   │   └── BaseEntity.cs
    │   ├── Interfaces/
    │   │   ├── IRepository.cs
    │   │   └── IDeviceAdapter.cs       # Phase 2
    │   ├── Models/
    │   │   ├── Result.cs
    │   │   └── ApiResponse.cs
    │   └── Exceptions/
    │       ├── NotFoundException.cs
    │       └── ValidationException.cs
    └── Infrastructure/
        ├── Infrastructure.csproj
        ├── Data/
        │   ├── AppDbContext.cs
        │   └── Repositories/
        │       └── GenericRepository.cs
        ├── Services/
        │   └── TokenService.cs
        └── Adapters/                   # Phase 2
            ├── AdapterFactory.cs
            ├── OpcUaAdapter.cs
            └── ModbusAdapter.cs
```

---

## Phase 1 — The Scaffold (~1 week)

**Goal:** `git clone → configure .env → docker compose up` gives any client a running API + Postgres stack.

### Bootstrap Commands

```bash
# Create solution
mkdir dotnet-forge && cd dotnet-forge
dotnet new sln -n dotnet-forge

# Create projects
dotnet new webapi -n Api -o src/Api --no-openapi
dotnet new classlib -n Core -o src/Core
dotnet new classlib -n Infrastructure -o src/Infrastructure

# Wire references
dotnet sln add src/Api src/Core src/Infrastructure
dotnet add src/Api reference src/Core src/Infrastructure
dotnet add src/Infrastructure reference src/Core

# Packages — Api
dotnet add src/Api package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add src/Api package Swashbuckle.AspNetCore
dotnet add src/Api package Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore

# Packages — Infrastructure
dotnet add src/Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/Infrastructure package Microsoft.EntityFrameworkCore.Design
dotnet add src/Infrastructure package Microsoft.EntityFrameworkCore.Tools

# EF Core CLI tool (global, one-time)
dotnet tool install --global dotnet-ef

# First migration (run after AppDbContext is wired)
dotnet ef migrations add InitialCreate -p src/Infrastructure -s src/Api
dotnet ef database update -p src/Infrastructure -s src/Api
```

---

### `src/Core/Entities/BaseEntity.cs`

Every entity inherits this. `UpdatedAt` is auto-patched in `AppDbContext.SaveChangesAsync`.

```csharp
namespace Core.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

---

### `src/Core/Models/Result.cs`

Railway-oriented result type. Services return this — never throw across layer boundaries.

```csharp
namespace Core.Models;

public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }

    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);

    public Result<TNext> Map<TNext>(Func<T, TNext> mapper) =>
        IsSuccess
            ? Result<TNext>.Success(mapper(Value!))
            : Result<TNext>.Failure(Error!);
}
```

---

### `src/Core/Interfaces/IRepository.cs`

Infrastructure implements this. Api only ever depends on the interface — swap implementations freely.

```csharp
namespace Core.Interfaces;

public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default);
    Task<T> AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
}
```

---

### `src/Core/Exceptions/`

Thrown in Infrastructure, caught in `ExceptionMiddleware`, mapped to HTTP automatically. Nothing else needed.

```csharp
namespace Core.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string entityName, object id)
        : base($"{entityName} with id '{id}' was not found.") { }
}

public class ValidationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(string field, string message)
        : base("One or more validation errors occurred.")
    {
        Errors = new Dictionary<string, string[]> { { field, [message] } };
    }

    public ValidationException(Dictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }
}
```

---

### `src/Infrastructure/Data/AppDbContext.cs`

`ApplyConfigurationsFromAssembly` auto-discovers any `IEntityTypeConfiguration<T>` you drop in — keeps `OnModelCreating` clean as the schema grows.

```csharp
namespace Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Widget> Widgets => Set<Widget>();
    // Phase 2: public DbSet<SensorReading> SensorReadings => Set<SensorReading>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;

        return await base.SaveChangesAsync(ct);
    }
}
```

---

### `src/Infrastructure/Data/Repositories/GenericRepository.cs`

For resource-specific queries, extend this: `class WidgetRepository : GenericRepository<Widget>` and add your custom methods.

```csharp
namespace Infrastructure.Data.Repositories;

public class GenericRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext _ctx;
    protected readonly DbSet<T> _set;

    public GenericRepository(AppDbContext ctx)
    {
        _ctx = ctx;
        _set = ctx.Set<T>();
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _set.FindAsync([id], ct);

    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default) =>
        await _set.AsNoTracking().ToListAsync(ct);

    public async Task<IEnumerable<T>> FindAsync(
        Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        await _set.AsNoTracking().Where(predicate).ToListAsync(ct);

    public async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        await _set.AddAsync(entity, ct);
        await _ctx.SaveChangesAsync(ct);
        return entity;
    }

    public async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        _set.Update(entity);
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetByIdAsync(id, ct)
            ?? throw new NotFoundException(typeof(T).Name, id);
        _set.Remove(entity);
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
        await _set.AnyAsync(e => e.Id == id, ct);
}
```

---

### `src/Infrastructure/Services/TokenService.cs`

Raw JWT — no ASP.NET Identity, no bloat. Validate on the way in, generate on the way out.

```csharp
namespace Infrastructure.Services;

public class TokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config) => _config = config;

    public string GenerateToken(string userId, string email, IEnumerable<string> roles)
    {
        var secret = _config["Jwt:Secret"]
            ?? throw new InvalidOperationException("JWT secret not configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, email),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

---

### `src/Api/Middleware/ExceptionMiddleware.cs`

Maps `Core.Exceptions` to RFC 7807 Problem Details. Stack traces only leak in Development.

```csharp
namespace Api.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await _next(ctx); }
        catch (NotFoundException ex)
        {
            await WriteProblem(ctx, 404, "Not Found", ex.Message);
        }
        catch (ValidationException ex)
        {
            ctx.Response.StatusCode = 422;
            ctx.Response.ContentType = "application/problem+json";
            await ctx.Response.WriteAsJsonAsync(new
            {
                title = "Validation Error",
                status = 422,
                errors = ex.Errors,
                instance = ctx.Request.Path.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                ctx.Request.Method, ctx.Request.Path);

            var detail = _env.IsDevelopment() ? ex.ToString() : "An unexpected error occurred.";
            await WriteProblem(ctx, 500, "Server Error", detail);
        }
    }

    private static async Task WriteProblem(HttpContext ctx, int status, string title, string detail)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        await ctx.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = ctx.Request.Path
        });
    }
}
```

---

### `src/Api/Controllers/BaseApiController.cs`

Consistent response envelope. `FromResult<T>` unwraps a `Result<T>` into an HTTP response in one line.

```csharp
namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    protected ActionResult<ApiResponse<T>> OkResult<T>(T data, string? message = null) =>
        Ok(new ApiResponse<T>(true, data, message));

    protected ActionResult<ApiResponse<T>> CreatedResult<T>(
        string routeName, object routeValues, T data) =>
        CreatedAtRoute(routeName, routeValues, new ApiResponse<T>(true, data));

    protected ActionResult<ApiResponse<T>> FromResult<T>(Result<T> result) =>
        result.IsSuccess
            ? OkResult(result.Value!)
            : BadRequest(new ProblemDetails
            {
                Detail = result.Error,
                Status = 400,
                Instance = HttpContext.Request.Path
            });
}

public record ApiResponse<T>(bool Success, T Data, string? Message = null);
```

---

### `src/Api/Extensions/ServiceCollectionExtensions.cs`

Keeps `Program.cs` clean. Add extension methods here as the feature surface grows.

```csharp
namespace Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services, IConfiguration config)
    {
        var secret = config["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret not configured.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    ValidateIssuer = true,
                    ValidIssuer = config["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = config["Jwt:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero   // no grace period — token expires when it says it does
                };
            });

        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("DefaultConnection")));

        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
        services.AddScoped<TokenService>();

        return services;
    }

    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header. Example: 'Bearer {token}'",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {{
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }});
        });
        return services;
    }
}
```

---

### `src/Api/Program.cs`

```csharp
using Api.Extensions;
using Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithJwt();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services
    .AddHealthChecks()
    .AddNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")!);

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

// Auto-run migrations on startup (optional — remove for prod if you prefer manual)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
```

---

### `Dockerfile`

Multi-stage build. Final image is the runtime only — no SDK bloat in production.

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/Api/Api.csproj", "Api/"]
COPY ["src/Core/Core.csproj", "Core/"]
COPY ["src/Infrastructure/Infrastructure.csproj", "Infrastructure/"]
RUN dotnet restore "Api/Api.csproj"

COPY src/ .
WORKDIR /src/Api
RUN dotnet publish "Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Api.dll"]
```

---

### `docker-compose.yml`

```yaml
services:
  api:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "${API_PORT:-5000}:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: ${ASPNETCORE_ENVIRONMENT:-Production}
      ConnectionStrings__DefaultConnection: >
        Host=db;Database=${POSTGRES_DB};
        Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
      Jwt__Secret: ${JWT_SECRET}
      Jwt__Issuer: ${JWT_ISSUER}
      Jwt__Audience: ${JWT_AUDIENCE}
    depends_on:
      db:
        condition: service_healthy
    restart: unless-stopped

  db:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: ${POSTGRES_DB}
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./scripts/seed.sql:/docker-entrypoint-initdb.d/seed.sql
    ports:
      - "${DB_PORT:-5432}:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER} -d ${POSTGRES_DB}"]
      interval: 5s
      timeout: 5s
      retries: 5
    restart: unless-stopped

volumes:
  postgres_data:
```

### `docker-compose.override.yml`

```yaml
# Dev only — not committed with sensitive data
services:
  api:
    environment:
      ASPNETCORE_ENVIRONMENT: Development
    volumes:
      - ./src:/src   # hot reload with dotnet watch

  pgadmin:
    image: dpage/pgadmin4
    profiles: ["dev"]
    environment:
      PGADMIN_DEFAULT_EMAIL: ${PGADMIN_EMAIL:-admin@local.dev}
      PGADMIN_DEFAULT_PASSWORD: ${PGADMIN_PASSWORD:-admin}
    ports:
      - "5050:80"
    depends_on:
      - db
```

### `.env.example`

```
# App
ASPNETCORE_ENVIRONMENT=Development
API_PORT=5000

# Database
POSTGRES_DB=dotnetforge
POSTGRES_USER=forge
POSTGRES_PASSWORD=changeme_in_prod
DB_PORT=5432

# JWT — minimum 32 chars for HS256
JWT_SECRET=replace_this_with_a_real_secret_min_32_chars
JWT_ISSUER=dotnet-forge
JWT_AUDIENCE=dotnet-forge-clients

# Dev tools (pgAdmin — only with: docker compose --profile dev up)
PGADMIN_EMAIL=admin@local.dev
PGADMIN_PASSWORD=admin
```

---

### Phase 1 Deliverable

```bash
git clone <your-template>
cp .env.example .env   # fill in secrets
docker compose up --build
# → API running at localhost:5000
# → Swagger at localhost:5000/swagger
# → Health check at localhost:5000/health
# → Postgres at localhost:5432
```

---

---

## Phase 2 — Manufacturing Hooks (~2 weeks)

**Goal:** Plug in a Modbus simulator or OPC-UA server. API reads tags, broadcasts over SignalR, persists to Postgres.

### New Packages

```bash
dotnet add src/Infrastructure package OPCFoundation.NetStandard.Opc.Ua
dotnet add src/Infrastructure package NModbus
dotnet add src/Api package Microsoft.AspNetCore.SignalR
```

---

### `src/Core/Interfaces/IDeviceAdapter.cs`

The abstraction everything else depends on. OPC-UA, Modbus, and any future protocol implement this. Your `Infrastructure` layer wires the concrete class; your `Api` layer only ever sees `IDeviceAdapter`.

```csharp
namespace Core.Interfaces;

public interface IDeviceAdapter : IAsyncDisposable
{
    string AdapterId { get; }
    bool IsConnected { get; }

    Task ConnectAsync(AdapterConfig config, CancellationToken ct = default);
    Task<TagValue> ReadTagAsync(string tagAddress, CancellationToken ct = default);
    Task WriteTagAsync(string tagAddress, object value, CancellationToken ct = default);
    IAsyncEnumerable<TagValue> SubscribeAsync(string tagAddress, CancellationToken ct = default);
    Task DisconnectAsync();
}

public record AdapterConfig(
    string Host,
    int Port,
    string Protocol,            // "opcua" | "modbus"
    TimeSpan PollInterval,
    Dictionary<string, string>? Options = null);

public record TagValue(
    string AdapterId,
    string TagAddress,
    object Value,
    DateTime Timestamp,
    string Unit = "");
```

---

### `src/Infrastructure/Adapters/ModbusAdapter.cs`

```csharp
namespace Infrastructure.Adapters;

public class ModbusAdapter : IDeviceAdapter
{
    private IModbusMaster? _master;
    private TcpClient? _client;
    private AdapterConfig? _config;

    public string AdapterId { get; private set; } = string.Empty;
    public bool IsConnected => _client?.Connected ?? false;

    public async Task ConnectAsync(AdapterConfig config, CancellationToken ct = default)
    {
        _config = config;
        AdapterId = $"modbus-{config.Host}:{config.Port}";
        _client = new TcpClient();
        await _client.ConnectAsync(config.Host, config.Port, ct);
        _master = ModbusIpMaster.CreateIp(_client);
    }

    public async Task<TagValue> ReadTagAsync(string tagAddress, CancellationToken ct = default)
    {
        // tagAddress format: "HR:0:1" = holding registers, start 0, count 1
        var parts = tagAddress.Split(':');
        var type = parts[0];    // HR, CO, DI, IR
        var start = ushort.Parse(parts[1]);
        var count = ushort.Parse(parts.Length > 2 ? parts[2] : "1");

        object value = type switch
        {
            "HR" => await _master!.ReadHoldingRegistersAsync(1, start, count),
            "CO" => await _master!.ReadCoilsAsync(1, start, count),
            "DI" => await _master!.ReadInputsAsync(1, start, count),
            "IR" => await _master!.ReadInputRegistersAsync(1, start, count),
            _ => throw new ArgumentException($"Unknown register type: {type}")
        };

        return new TagValue(AdapterId, tagAddress, value, DateTime.UtcNow);
    }

    public async Task WriteTagAsync(string tagAddress, object value, CancellationToken ct = default)
    {
        var parts = tagAddress.Split(':');
        var type = parts[0];
        var address = ushort.Parse(parts[1]);

        if (type == "HR")
            await _master!.WriteSingleRegisterAsync(1, address, Convert.ToUInt16(value));
        else if (type == "CO")
            await _master!.WriteSingleCoilAsync(1, address, Convert.ToBoolean(value));
        else
            throw new NotSupportedException($"Write not supported for register type: {type}");
    }

    public async IAsyncEnumerable<TagValue> SubscribeAsync(
        string tagAddress,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            yield return await ReadTagAsync(tagAddress, ct);
            await Task.Delay(_config!.PollInterval, ct);
        }
    }

    public Task DisconnectAsync()
    {
        _master?.Dispose();
        _client?.Close();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        DisconnectAsync();
        return ValueTask.CompletedTask;
    }
}
```

---

### `src/Infrastructure/Adapters/AdapterFactory.cs`

DI-registered singleton. Register adapters by config at startup; poll service uses this to iterate active adapters.

```csharp
namespace Infrastructure.Adapters;

public class AdapterFactory
{
    private readonly IServiceProvider _sp;
    private readonly ConcurrentDictionary<string, IDeviceAdapter> _adapters = new();

    public AdapterFactory(IServiceProvider sp) => _sp = sp;

    public async Task<IDeviceAdapter> RegisterAsync(AdapterConfig config)
    {
        IDeviceAdapter adapter = config.Protocol.ToLower() switch
        {
            "modbus" => new ModbusAdapter(),
            "opcua"  => new OpcUaAdapter(),
            _ => throw new ArgumentException($"Unknown protocol: {config.Protocol}")
        };

        await adapter.ConnectAsync(config);
        _adapters[adapter.AdapterId] = adapter;
        return adapter;
    }

    public IEnumerable<IDeviceAdapter> GetAll() => _adapters.Values;

    public IDeviceAdapter? Get(string adapterId) =>
        _adapters.TryGetValue(adapterId, out var adapter) ? adapter : null;
}
```

---

### `src/Core/Entities/SensorReading.cs`

```csharp
namespace Core.Entities;

public class SensorReading : BaseEntity
{
    public string AdapterId { get; set; } = string.Empty;
    public string TagAddress { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;       // stored as string; cast on read
    public string Unit { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
```

---

### `src/Api/Hubs/DeviceDataHub.cs`

Clients subscribe to a tag-scoped group. Polling service broadcasts into the group — subscribers get updates, everyone else doesn't.

```csharp
namespace Api.Hubs;

[Authorize]
public class DeviceDataHub : Hub
{
    public async Task SubscribeToTag(string adapterId, string tagAddress) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupKey(adapterId, tagAddress));

    public async Task UnsubscribeFromTag(string adapterId, string tagAddress) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupKey(adapterId, tagAddress));

    private static string GroupKey(string adapterId, string tagAddress) =>
        $"{adapterId}::{tagAddress}";
}
```

---

### `src/Infrastructure/Services/PollingBackgroundService.cs`

`BackgroundService` runs on startup. Iterates every registered adapter, reads subscribed tags, persists to Postgres, and broadcasts via SignalR.

```csharp
namespace Infrastructure.Services;

public class PollingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<DeviceDataHub> _hub;
    private readonly AdapterFactory _adapterFactory;
    private readonly ILogger<PollingBackgroundService> _logger;

    public PollingBackgroundService(
        IServiceScopeFactory scopeFactory,
        IHubContext<DeviceDataHub> hub,
        AdapterFactory adapterFactory,
        ILogger<PollingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
        _adapterFactory = adapterFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var adapter in _adapterFactory.GetAll())
            {
                if (!adapter.IsConnected) continue;

                // Tags to poll come from AdapterConfig.Options["tags"] = "HR:0,HR:1,CO:5"
                var tags = GetSubscribedTags(adapter.AdapterId);

                foreach (var tag in tags)
                {
                    try
                    {
                        var reading = await adapter.ReadTagAsync(tag, stoppingToken);

                        using var scope = _scopeFactory.CreateScope();
                        var repo = scope.ServiceProvider
                            .GetRequiredService<IRepository<SensorReading>>();

                        await repo.AddAsync(new SensorReading
                        {
                            AdapterId = reading.AdapterId,
                            TagAddress = reading.TagAddress,
                            Value = reading.Value.ToString()!,
                            Unit = reading.Unit,
                            RecordedAt = reading.Timestamp
                        }, stoppingToken);

                        await _hub.Clients
                            .Group($"{reading.AdapterId}::{tag}")
                            .SendAsync("TagUpdate", reading, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Error polling tag {Tag} on adapter {Adapter}", tag, adapter.AdapterId);
                    }
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    // Wire this to your DB-stored adapter configs in Phase 3
    private static IEnumerable<string> GetSubscribedTags(string adapterId) => [];
}
```

---

### Phase 2 Deliverable

```bash
# Point at a Modbus simulator (e.g. diagslave, ModRSsim2 in Wine)
# or an OPC-UA server (e.g. Prosys OPC UA Simulation Server — free)

# Register an adapter via API:
POST /api/adapters
{
  "host": "192.168.1.50",
  "port": 502,
  "protocol": "modbus",
  "pollIntervalSeconds": 1,
  "tags": ["HR:0", "HR:1", "CO:5"]
}

# Connect a SignalR client, subscribe to a tag, watch live updates roll in.
# All readings persisted to SensorReadings table in Postgres.
```

---

---

## Phase 3 — The Client Kit (~1 week)

**Goal:** One script, a few prompts, a fully configured client stack ready to point at their equipment.

### New Packages

```bash
dotnet add src/Infrastructure package MailKit          # email notifications
# Slack and generic webhook use HttpClient — no extra package needed
```

---

### `src/Core/Entities/AlertRule.cs`

```csharp
namespace Core.Entities;

public class AlertRule : BaseEntity
{
    public string AdapterId { get; set; } = string.Empty;
    public string TagAddress { get; set; } = string.Empty;
    public AlertCondition Condition { get; set; }
    public double Threshold { get; set; }
    public TimeSpan Cooldown { get; set; } = TimeSpan.FromMinutes(5);
    public bool IsEnabled { get; set; } = true;
    public string NotificationChannels { get; set; } = "[]";  // JSON: ["email","slack"]
    public DateTime? LastTriggeredAt { get; set; }
}

public enum AlertCondition { GreaterThan, LessThan, Equal, NotEqual }
```

---

### `src/Core/Interfaces/INotificationChannel.cs`

```csharp
namespace Core.Interfaces;

public interface INotificationChannel
{
    string ChannelType { get; }
    Task SendAsync(AlertEvent alert, CancellationToken ct = default);
}

public class AlertEvent
{
    public Guid RuleId { get; set; }
    public string AdapterId { get; set; } = string.Empty;
    public string TagAddress { get; set; } = string.Empty;
    public object TriggeredValue { get; set; } = new();
    public AlertCondition Condition { get; set; }
    public double Threshold { get; set; }
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;

    public string Message =>
        $"[ALERT] {TagAddress} is {TriggeredValue} " +
        $"(rule: {Condition} {Threshold}) @ {TriggeredAt:u}";
}
```

---

### `src/Infrastructure/Services/AlertEvaluator.cs`

Plugged into `PollingBackgroundService` — called after each tag read. Checks enabled rules, respects cooldown, dispatches notifications.

```csharp
namespace Infrastructure.Services;

public class AlertEvaluator
{
    private readonly IRepository<AlertRule> _rules;
    private readonly NotificationDispatcher _dispatcher;

    public AlertEvaluator(IRepository<AlertRule> rules, NotificationDispatcher dispatcher)
    {
        _rules = rules;
        _dispatcher = dispatcher;
    }

    public async Task EvaluateAsync(TagValue reading, CancellationToken ct = default)
    {
        if (!double.TryParse(reading.Value.ToString(), out var numericValue)) return;

        var matchingRules = await _rules.FindAsync(r =>
            r.AdapterId == reading.AdapterId &&
            r.TagAddress == reading.TagAddress &&
            r.IsEnabled, ct);

        foreach (var rule in matchingRules)
        {
            if (!IsTriggered(rule.Condition, numericValue, rule.Threshold)) continue;
            if (rule.LastTriggeredAt.HasValue &&
                DateTime.UtcNow - rule.LastTriggeredAt.Value < rule.Cooldown) continue;

            var alert = new AlertEvent
            {
                RuleId = rule.Id,
                AdapterId = reading.AdapterId,
                TagAddress = reading.TagAddress,
                TriggeredValue = reading.Value,
                Condition = rule.Condition,
                Threshold = rule.Threshold
            };

            await _dispatcher.DispatchAsync(alert,
                JsonSerializer.Deserialize<List<string>>(rule.NotificationChannels) ?? [],
                ct);

            rule.LastTriggeredAt = DateTime.UtcNow;
            await _rules.UpdateAsync(rule, ct);
        }
    }

    private static bool IsTriggered(AlertCondition condition, double value, double threshold) =>
        condition switch
        {
            AlertCondition.GreaterThan => value > threshold,
            AlertCondition.LessThan   => value < threshold,
            AlertCondition.Equal      => Math.Abs(value - threshold) < 0.0001,
            AlertCondition.NotEqual   => Math.Abs(value - threshold) >= 0.0001,
            _ => false
        };
}
```

---

### `src/Infrastructure/Services/NotificationDispatcher.cs`

Fan-out to all configured channels. Adding a new channel type = implement `INotificationChannel`, register it, done.

```csharp
namespace Infrastructure.Services;

public class NotificationDispatcher
{
    private readonly IEnumerable<INotificationChannel> _channels;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        IEnumerable<INotificationChannel> channels,
        ILogger<NotificationDispatcher> logger)
    {
        _channels = channels;
        _logger = logger;
    }

    public async Task DispatchAsync(
        AlertEvent alert,
        IEnumerable<string> channelTypes,
        CancellationToken ct = default)
    {
        var targets = _channels
            .Where(c => channelTypes.Contains(c.ChannelType))
            .ToList();

        await Task.WhenAll(targets.Select(async channel =>
        {
            try { await channel.SendAsync(alert, ct); }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Notification failed on channel {Channel}", channel.ChannelType);
            }
        }));
    }
}
```

---

### `scripts/scaffold-client.sh`

Run once per new client engagement. Clones the template, renames the solution, and generates a `.env` interactively.

```bash
#!/usr/bin/env bash
set -euo pipefail

TEMPLATE_REPO="https://github.com/yourname/dotnet-forge"

echo "╔══════════════════════════════════╗"
echo "║   dotnet-forge client scaffolder ║"
echo "╚══════════════════════════════════╝"

read -rp "Client name (slug, no spaces): " CLIENT_NAME
read -rp "Postgres password: " -s DB_PASS; echo
read -rp "JWT secret (min 32 chars): " -s JWT_SECRET; echo
read -rp "API port [5000]: " API_PORT
API_PORT="${API_PORT:-5000}"

TARGET_DIR="${CLIENT_NAME}-api"
git clone "$TEMPLATE_REPO" "$TARGET_DIR"
cd "$TARGET_DIR"

# Rename solution files
find . -name "*.sln" -exec sed -i "s/dotnet-forge/${CLIENT_NAME}/g" {} \;
find . -name "*.csproj" -exec sed -i "s/dotnet-forge/${CLIENT_NAME}/g" {} \;

# Write .env
cat > .env <<EOF
ASPNETCORE_ENVIRONMENT=Production
API_PORT=${API_PORT}
POSTGRES_DB=${CLIENT_NAME}
POSTGRES_USER=${CLIENT_NAME}_user
POSTGRES_PASSWORD=${DB_PASS}
DB_PORT=5432
JWT_SECRET=${JWT_SECRET}
JWT_ISSUER=${CLIENT_NAME}-api
JWT_AUDIENCE=${CLIENT_NAME}-clients
EOF

echo ""
echo "✓ Scaffolded → ./${TARGET_DIR}"
echo "  Next: cd ${TARGET_DIR} && docker compose up --build"
```

---

### Phase 3 Deliverable

```bash
chmod +x scripts/scaffold-client.sh
./scripts/scaffold-client.sh

# → Client name: acme-manufacturing
# → Fills prompts
# → Cloned, renamed, .env written
# → cd acme-manufacturing-api && docker compose up --build
# → Running, secured, ready to point at their floor equipment
```

---

## JS → C# Quick Reference

| JavaScript / TypeScript | C# Equivalent |
|---|---|
| `Express route handler` | `Controller action + [HttpGet]` |
| `app.use()` middleware | `app.UseMiddleware<T>()` |
| `res.json()` / `res.status()` | `return Ok(data)` / `return NotFound()` |
| `async/await` | `async Task<T>` / `await` — same concept, stricter typing |
| `process.env.X` | `IConfiguration["Section:Key"]` or Options pattern |
| TypeScript `interface` | `interface` — used pervasively for DI, not just typing |
| Prisma schema + migrate | `DbContext` + `dotnet ef migrations add` |
| `npm install` | `dotnet add package` (NuGet) |
| `nodemon` | `dotnet watch run` |
| Jest / Playwright | xUnit + Moq for unit; Playwright still valid for E2E |
| `npm init` | `dotnet new` |