using Core.Interfaces;
using Infrastructure.Adapters;
using Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// SQLite connection kept open for the lifetime of the factory so the
    /// in-memory database persists across scopes.
    /// </summary>
    private SqliteConnection? _connection;

    /// <summary>
    /// Shared mock notification channel accessible from tests to inspect dispatched alerts.
    /// </summary>
    public MockNotificationChannel MockChannel { get; } = new("mock");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // ── Configuration must be set before services are wired ─────────
        builder.UseSetting("Jwt:Secret", "integration-test-secret-key-that-is-at-least-32-chars!");
        builder.UseSetting("Jwt:Issuer", "dotnet-forge-test");
        builder.UseSetting("Jwt:Audience", "dotnet-forge-test");
        builder.UseSetting("Polling:IntervalSeconds", "0.2");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "DataSource=:memory:");

        builder.ConfigureServices(services =>
        {
            // ── Replace Postgres with SQLite in-memory ──────────────────
            var dbDescriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>))
                .ToList();
            foreach (var d in dbDescriptors) services.Remove(d);

            // Remove all DbContextOptions registrations
            var optionsDescriptors = services
                .Where(d => d.ServiceType.IsGenericType
                         && d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>))
                .ToList();
            foreach (var d in optionsDescriptors) services.Remove(d);

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(_connection));

            // ── Remove the NpgSql health check ──────────────────────────
            var healthCheckDescriptors = services
                .Where(d => d.ServiceType.FullName?.Contains("HealthCheck") == true)
                .ToList();
            foreach (var d in healthCheckDescriptors) services.Remove(d);
            services.AddHealthChecks();

            // ── Register mock protocol on AdapterFactory ────────────────
            // Replace the existing singleton with a pre-configured instance
            // so that the "mock" protocol is available at runtime.
            var afDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(AdapterFactory));
            if (afDescriptor != null) services.Remove(afDescriptor);

            var adapterFactory = new AdapterFactory();
            adapterFactory.RegisterProtocol("mock", () => new MockDeviceAdapter());
            services.AddSingleton(adapterFactory);

            // ── Replace real notification channels with mock ─────────────
            var channelDescriptors = services
                .Where(d => d.ServiceType == typeof(INotificationChannel))
                .ToList();
            foreach (var d in channelDescriptors) services.Remove(d);

            services.AddSingleton<INotificationChannel>(MockChannel);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection?.Dispose();
        }
    }
}
