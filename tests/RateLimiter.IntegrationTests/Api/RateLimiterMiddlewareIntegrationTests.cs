using System.Net;
using System.Text.Json;
using RateLimiter.Domain.Entities;
using RateLimiter.Domain.Enums;
using RateLimiter.IntegrationTests.Fixtures;

namespace RateLimiter.IntegrationTests.Api;

[Collection(RedisCollection.Name)]
public sealed class RateLimiterMiddlewareIntegrationTests : IAsyncLifetime
{
    private readonly RedisFixture _fixture;

    private static readonly RateLimitRule TestRule = new()
    {
        Domain = "api",
        Descriptor = "endpoint",
        DescriptorValue = "/Hello",
        Unit = TimeUnit.Second,
        RequestsPerUnit = 3,
        Algorithm = RateLimitAlgorithm.FixedWindowCounter,
    };

    public RateLimiterMiddlewareIntegrationTests(RedisFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.FlushDb();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Request_ReturnsRateLimitHeaders()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "header-test");

        var response = await client.GetAsync("/Hello");

        Assert.True(response.Headers.Contains("X-Ratelimit-Limit"));
        Assert.True(response.Headers.Contains("X-Ratelimit-Remaining"));
    }

    [Fact]
    public async Task RequestWithinLimit_Returns200()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "ok-test");

        var response = await client.GetAsync("/Hello");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RequestExceedingLimit_Returns429()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "exceed-test");

        for (var i = 0; i < 3; i++)
            await client.GetAsync("/Hello");

        var response = await client.GetAsync("/Hello");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        Assert.Equal("Too Many Requests", json.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task RequestExceedingLimit_Returns429WithRetryAfterHeader()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "retry-header-test");

        for (var i = 0; i < 3; i++)
            await client.GetAsync("/Hello");

        var response = await client.GetAsync("/Hello");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Ratelimit-Retry-After"));
    }

    [Fact]
    public async Task RateLimiterDisabled_AllRequestsPass()
    {
        await using var factory = new RateLimiterWebApplicationFactory(
            _fixture.Connection,
            [TestRule],
            opts => opts.Enabled = false);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "disabled-test");

        for (var i = 0; i < 10; i++)
        {
            var response = await client.GetAsync("/Hello");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    [Fact]
    public async Task NoMatchingRule_AllowsRequest()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "no-rule-test");

        // Request an endpoint that has no rule defined
        var response = await client.GetAsync("/api/unmatched");

        Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task DifferentClients_HaveIndependentLimits()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        // Exhaust limit for client A
        client.DefaultRequestHeaders.Remove("X-Api-Key");
        client.DefaultRequestHeaders.Add("X-Api-Key", "client-a");
        for (var i = 0; i < 3; i++)
            await client.GetAsync("/Hello");

        var rejectedA = await client.GetAsync("/Hello");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedA.StatusCode);

        // Client B should still be allowed
        client.DefaultRequestHeaders.Remove("X-Api-Key");
        client.DefaultRequestHeaders.Add("X-Api-Key", "client-b");
        var responseB = await client.GetAsync("/Hello");
        Assert.Equal(HttpStatusCode.OK, responseB.StatusCode);
    }

    [Fact]
    public async Task FailOpen_WhenRedisUnavailable()
    {
        // Use a bogus Redis endpoint that will fail to connect
        var bogusConfig = new StackExchange.Redis.ConfigurationOptions
        {
            EndPoints = { "localhost:59999" },
            ConnectTimeout = 1000,
            SyncTimeout = 1000,
            AbortOnConnectFail = false,
        };
        var bogusConnection = await StackExchange.Redis.ConnectionMultiplexer.ConnectAsync(bogusConfig);

        await using var factory = new RateLimiterWebApplicationFactory(
            bogusConnection, [TestRule]);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "failopen-test");

        // Should succeed (fail-open) even though Redis is unreachable
        var response = await client.GetAsync("/Hello");
        Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);

        bogusConnection.Dispose();
    }

    private RateLimiterWebApplicationFactory CreateFactory() =>
        new(_fixture.Connection, [TestRule]);
}
