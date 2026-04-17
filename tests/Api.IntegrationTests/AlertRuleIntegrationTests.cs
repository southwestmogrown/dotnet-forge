using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Api.Controllers;
using Core.Entities;
using Core.Models;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Api.IntegrationTests;

public class AlertRuleIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private const string JwtSecret = "integration-test-secret-key-that-is-at-least-32-chars!";
    private const string JwtIssuer = "dotnet-forge-test";
    private const string JwtAudience = "dotnet-forge-test";
    private const int MaxPollingAttempts = 25;
    private const int PollingDelayMs = 200;

    public AlertRuleIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateTestJwt());
    }

    // ── CRUD Tests ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Post_AlertRule_Then_Get_Returns_Created_Rule()
    {
        // Arrange
        var request = new AlertRuleRequest(
            AdapterId: "test-adapter",
            TagAddress: "HR:0:1",
            Condition: AlertCondition.GreaterThan,
            Threshold: 100.0,
            CooldownSeconds: 60,
            IsEnabled: true,
            NotificationChannels: new[] { "mock" });

        // Act – Create
        var postResponse = await _client.PostAsJsonAsync("/api/alertrules", request);
        postResponse.EnsureSuccessStatusCode();

        var postBody = await postResponse.Content.ReadFromJsonAsync<ApiResponse<AlertRule>>();
        Assert.NotNull(postBody);
        Assert.True(postBody.Success);
        Assert.Equal("test-adapter", postBody.Data.AdapterId);
        Assert.Equal("HR:0:1", postBody.Data.TagAddress);
        Assert.Equal(AlertCondition.GreaterThan, postBody.Data.Condition);
        Assert.Equal(100.0, postBody.Data.Threshold);
        Assert.True(postBody.Data.IsEnabled);

        // Act – Get by ID
        var getResponse = await _client.GetAsync($"/api/alertrules/{postBody.Data.Id}");
        getResponse.EnsureSuccessStatusCode();

        var getBody = await getResponse.Content.ReadFromJsonAsync<ApiResponse<AlertRule>>();
        Assert.NotNull(getBody);
        Assert.True(getBody.Success);
        Assert.Equal(postBody.Data.Id, getBody.Data.Id);
    }

    [Fact]
    public async Task Get_All_AlertRules_Returns_List()
    {
        // Arrange – create a rule so there's at least one
        var request = new AlertRuleRequest(
            AdapterId: "list-adapter",
            TagAddress: "HR:0:2",
            Condition: AlertCondition.LessThan,
            Threshold: 10.0,
            NotificationChannels: new[] { "mock" });

        var postResponse = await _client.PostAsJsonAsync("/api/alertrules", request);
        postResponse.EnsureSuccessStatusCode();

        // Act
        var getResponse = await _client.GetAsync("/api/alertrules");
        getResponse.EnsureSuccessStatusCode();

        var getBody = await getResponse.Content
            .ReadFromJsonAsync<ApiResponse<List<AlertRule>>>();
        Assert.NotNull(getBody);
        Assert.True(getBody.Success);
        Assert.NotEmpty(getBody.Data);
    }

    [Fact]
    public async Task Put_AlertRule_Updates_Fields()
    {
        // Arrange – create a rule
        var createReq = new AlertRuleRequest(
            AdapterId: "update-adapter",
            TagAddress: "HR:0:3",
            Condition: AlertCondition.Equal,
            Threshold: 50.0,
            NotificationChannels: new[] { "mock" });

        var postResponse = await _client.PostAsJsonAsync("/api/alertrules", createReq);
        postResponse.EnsureSuccessStatusCode();
        var created = (await postResponse.Content.ReadFromJsonAsync<ApiResponse<AlertRule>>())!.Data;

        // Act – update
        var updateReq = new AlertRuleRequest(
            AdapterId: "update-adapter",
            TagAddress: "HR:0:3",
            Condition: AlertCondition.NotEqual,
            Threshold: 75.0,
            CooldownSeconds: 120,
            IsEnabled: false,
            NotificationChannels: new[] { "mock" });

        var putResponse = await _client.PutAsJsonAsync($"/api/alertrules/{created.Id}", updateReq);
        putResponse.EnsureSuccessStatusCode();

        var putBody = await putResponse.Content.ReadFromJsonAsync<ApiResponse<AlertRule>>();
        Assert.NotNull(putBody);
        Assert.Equal(AlertCondition.NotEqual, putBody.Data.Condition);
        Assert.Equal(75.0, putBody.Data.Threshold);
        Assert.False(putBody.Data.IsEnabled);
    }

    [Fact]
    public async Task Delete_AlertRule_Removes_Rule()
    {
        // Arrange – create
        var request = new AlertRuleRequest(
            AdapterId: "delete-adapter",
            TagAddress: "HR:0:4",
            Condition: AlertCondition.GreaterThan,
            Threshold: 200.0,
            NotificationChannels: new[] { "mock" });

        var postResponse = await _client.PostAsJsonAsync("/api/alertrules", request);
        postResponse.EnsureSuccessStatusCode();
        var created = (await postResponse.Content.ReadFromJsonAsync<ApiResponse<AlertRule>>())!.Data;

        // Act – delete
        var deleteResponse = await _client.DeleteAsync($"/api/alertrules/{created.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        // Assert – get returns 404
        var getResponse = await _client.GetAsync($"/api/alertrules/{created.Id}");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    // ── Alert Evaluator Integration ─────────────────────────────────────────

    [Fact]
    public async Task AlertEvaluator_Triggers_When_Condition_Met()
    {
        // Arrange — register a mock adapter with a tag
        var adapterReq = new RegisterAdapterRequest(
            Host: "alert-host",
            Port: 5030,
            Protocol: "mock",
            PollIntervalSeconds: 1,
            Tags: new[] { "HR:0:1" });

        var adapterPost = await _client.PostAsJsonAsync("/api/adapters", adapterReq);
        adapterPost.EnsureSuccessStatusCode();
        var adapterBody = await adapterPost.Content.ReadFromJsonAsync<ApiResponse<AdapterDto>>();
        Assert.NotNull(adapterBody);
        var adapterId = adapterBody.Data.AdapterId;

        // Create an alert rule: MockDeviceAdapter returns 42.0, so GreaterThan 10 should trigger
        var ruleReq = new AlertRuleRequest(
            AdapterId: adapterId,
            TagAddress: "HR:0:1",
            Condition: AlertCondition.GreaterThan,
            Threshold: 10.0,
            CooldownSeconds: 0,  // No cooldown for test
            IsEnabled: true,
            NotificationChannels: new[] { "mock" });

        var rulePost = await _client.PostAsJsonAsync("/api/alertrules", ruleReq);
        rulePost.EnsureSuccessStatusCode();
        var ruleBody = await rulePost.Content.ReadFromJsonAsync<ApiResponse<AlertRule>>();
        Assert.NotNull(ruleBody);
        var ruleId = ruleBody.Data.Id;

        // Act — wait for the polling background service to trigger the alert
        AlertRule? updatedRule = null;
        for (var i = 0; i < MaxPollingAttempts; i++)
        {
            await Task.Delay(PollingDelayMs);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            updatedRule = db.AlertRules.FirstOrDefault(r => r.Id == ruleId);

            if (updatedRule?.LastTriggeredAt is not null) break;
        }

        // Assert — rule was triggered and mock channel received the alert
        Assert.NotNull(updatedRule);
        Assert.NotNull(updatedRule.LastTriggeredAt);
        Assert.Contains(_factory.MockChannel.SentAlerts, a => a.RuleId == ruleId);
    }

    [Fact]
    public async Task AlertEvaluator_Respects_Cooldown()
    {
        // Arrange — register adapter
        var adapterReq = new RegisterAdapterRequest(
            Host: "cooldown-host",
            Port: 5031,
            Protocol: "mock",
            PollIntervalSeconds: 1,
            Tags: new[] { "HR:0:1" });

        var adapterPost = await _client.PostAsJsonAsync("/api/adapters", adapterReq);
        adapterPost.EnsureSuccessStatusCode();
        var adapterBody = await adapterPost.Content.ReadFromJsonAsync<ApiResponse<AdapterDto>>();
        Assert.NotNull(adapterBody);
        var adapterId = adapterBody.Data.AdapterId;

        // Create rule with a very long cooldown (10 minutes)
        var ruleReq = new AlertRuleRequest(
            AdapterId: adapterId,
            TagAddress: "HR:0:1",
            Condition: AlertCondition.GreaterThan,
            Threshold: 10.0,
            CooldownSeconds: 600,  // 10 minute cooldown
            IsEnabled: true,
            NotificationChannels: new[] { "mock" });

        var rulePost = await _client.PostAsJsonAsync("/api/alertrules", ruleReq);
        rulePost.EnsureSuccessStatusCode();
        var ruleBody = await rulePost.Content.ReadFromJsonAsync<ApiResponse<AlertRule>>();
        Assert.NotNull(ruleBody);
        var ruleId = ruleBody.Data.Id;

        // Wait until the rule fires once
        AlertRule? rule = null;
        for (var i = 0; i < MaxPollingAttempts; i++)
        {
            await Task.Delay(PollingDelayMs);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            rule = db.AlertRules.FirstOrDefault(r => r.Id == ruleId);

            if (rule?.LastTriggeredAt is not null) break;
        }
        Assert.NotNull(rule?.LastTriggeredAt);

        // Record count after first trigger
        var countAfterFirst = _factory.MockChannel.SentAlerts.Count(a => a.RuleId == ruleId);

        // Wait for a few more polling cycles
        await Task.Delay(PollingDelayMs * 5);

        // Assert — count should not have increased due to cooldown
        var countAfterWait = _factory.MockChannel.SentAlerts.Count(a => a.RuleId == ruleId);
        Assert.Equal(countAfterFirst, countAfterWait);
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
