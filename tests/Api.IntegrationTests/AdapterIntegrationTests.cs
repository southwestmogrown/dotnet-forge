using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Api.Controllers;
using Core.Entities;
using Core.Models;
using Infrastructure.Adapters;
using Infrastructure.Data;
using Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Api.IntegrationTests;

public class AdapterIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private const string JwtSecret = "integration-test-secret-key-that-is-at-least-32-chars!";
    private const string JwtIssuer = "dotnet-forge-test";
    private const string JwtAudience = "dotnet-forge-test";
    private const int MaxPollingAttempts = 25;
    private const int PollingDelayMs = 200;
    private const int SignalRTimeoutSeconds = 10;

    public AdapterIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateTestJwt());
    }

    /// <summary>
    /// AC: POST /api/adapters with a mock adapter config succeeds
    ///     and adapter appears in GET /api/adapters
    /// </summary>
    [Fact]
    public async Task Post_Adapter_Then_Get_Returns_Registered_Adapter()
    {
        // Arrange
        var request = new RegisterAdapterRequest(
            Host: "test-host",
            Port: 5020,
            Protocol: "mock",
            PollIntervalSeconds: 1,
            Tags: new[] { "HR:0:1" });

        // Act – Register
        var postResponse = await _client.PostAsJsonAsync("/api/adapters", request);
        postResponse.EnsureSuccessStatusCode();

        var postBody = await postResponse.Content.ReadFromJsonAsync<ApiResponse<AdapterDto>>();
        Assert.NotNull(postBody);
        Assert.True(postBody.Success);
        Assert.Equal("mock", postBody.Data.Protocol);
        Assert.True(postBody.Data.IsConnected);

        // Act – List
        var getResponse = await _client.GetAsync("/api/adapters");
        getResponse.EnsureSuccessStatusCode();

        var getBody = await getResponse.Content
            .ReadFromJsonAsync<ApiResponse<List<AdapterDto>>>();
        Assert.NotNull(getBody);
        Assert.True(getBody.Success);
        Assert.Contains(getBody.Data, a => a.AdapterId == postBody.Data.AdapterId);
    }

    /// <summary>
    /// AC: After adapter registration, PollingBackgroundService produces at
    ///     least one SensorReading row within the poll interval.
    /// </summary>
    [Fact]
    public async Task Polling_Produces_SensorReading_After_Registration()
    {
        // Arrange – register a mock adapter with a tag
        var request = new RegisterAdapterRequest(
            Host: "poll-host",
            Port: 5021,
            Protocol: "mock",
            PollIntervalSeconds: 1,
            Tags: new[] { "HR:0:1" });

        var postResponse = await _client.PostAsJsonAsync("/api/adapters", request);
        postResponse.EnsureSuccessStatusCode();

        var postBody = await postResponse.Content.ReadFromJsonAsync<ApiResponse<AdapterDto>>();
        Assert.NotNull(postBody);

        var adapterId = postBody.Data.AdapterId;

        // Act – wait for the background service to poll (fast interval = 200ms)
        SensorReading? reading = null;
        for (var i = 0; i < MaxPollingAttempts; i++)
        {
            await Task.Delay(PollingDelayMs);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            reading = db.SensorReadings
                .FirstOrDefault(r => r.AdapterId == adapterId);

            if (reading is not null) break;
        }

        // Assert
        Assert.NotNull(reading);
        Assert.Equal(adapterId, reading.AdapterId);
        Assert.Equal("HR:0:1", reading.TagAddress);
    }

    /// <summary>
    /// AC: A SignalR test client subscribed to the registered tag receives
    ///     a TagUpdate message.
    /// </summary>
    [Fact]
    public async Task SignalR_Client_Receives_TagUpdate()
    {
        // Arrange – build a SignalR connection through the test server
        var hubConnection = new HubConnectionBuilder()
            .WithUrl(
                "http://localhost/hubs/device-data",
                opts =>
                {
                    opts.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    opts.AccessTokenProvider = () => Task.FromResult<string?>(GenerateTestJwt());
                })
            .Build();

        var tcs = new TaskCompletionSource<object>();
        hubConnection.On<object>("TagUpdate", value =>
        {
            tcs.TrySetResult(value);
        });

        await hubConnection.StartAsync();

        // Subscribe to the group matching the adapter + tag
        const string tag = "HR:0:1";

        // Register a mock adapter
        var request = new RegisterAdapterRequest(
            Host: "signalr-host",
            Port: 5022,
            Protocol: "mock",
            PollIntervalSeconds: 1,
            Tags: new[] { tag });

        var postResponse = await _client.PostAsJsonAsync("/api/adapters", request);
        postResponse.EnsureSuccessStatusCode();

        var postBody = await postResponse.Content.ReadFromJsonAsync<ApiResponse<AdapterDto>>();
        Assert.NotNull(postBody);
        var adapterId = postBody.Data.AdapterId;

        // Subscribe to the tag group
        await hubConnection.InvokeAsync("SubscribeToTag", adapterId, tag);

        // Act – wait for a TagUpdate message (polling is fast)
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(SignalRTimeoutSeconds)));

        // Assert
        Assert.Equal(tcs.Task, completed); // ensure it wasn't the timeout
        var receivedValue = await tcs.Task;
        Assert.NotNull(receivedValue);

        await hubConnection.DisposeAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string GenerateTestJwt()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim(ClaimTypes.Email, "test@dotnet-forge.local"),
        };

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
