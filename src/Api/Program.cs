using Api.Extensions;
using Api.Middleware;
using Core.Options;
using Infrastructure.Data;
using Infrastructure.Hubs;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 1_048_576);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithJwt();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddOptions<JwtOptions>().Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services
    .AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!);

builder.Services.AddRateLimiter(opts => opts
    .AddFixedWindowLimiter("default", opt => { opt.Window = TimeSpan.FromMinutes(1); opt.PermitLimit = 100; })
    .AddPolicy("auth", ctx => ctx.Connection.RemoteIpAddress != null
        ? RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress.ToString(), _ => new() { Window = TimeSpan.FromMinutes(1), PermitLimit = 5 })
        : RateLimitPartition.GetNoLimiter("anonymous"))
);

var app = builder.Build();

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<DeviceDataHub>("/hubs/device-data");
app.MapHealthChecks("/health");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();

// Make the auto-generated Program class accessible for WebApplicationFactory<Program> in integration tests
public partial class Program { }
