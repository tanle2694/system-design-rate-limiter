using System.Net;
using RateLimiter.Domain.Entities;
using RateLimiter.Domain.Enums;
using RateLimiter.IntegrationTests.Fixtures;

namespace RateLimiter.IntegrationTests.Api;

[Collection(RedisCollection.Name)]
public sealed class ClientIdentificationIntegrationTests : IAsyncLifetime
{
    private readonly RedisFixture _fixture;

    private static readonly RateLimitRule TestRule = new()
    {
        Domain = "api",
        Descriptor = "endpoint",
        DescriptorValue = "/Hello",
        Unit = TimeUnit.Second,
        RequestsPerUnit = 2,
        Algorithm = RateLimitAlgorithm.FixedWindowCounter,
    };

    public ClientIdentificationIntegrationTests(RedisFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.FlushDb();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task XForwardedFor_UsedAsClientId()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.1");

        // Exhaust limit
        for (var i = 0; i < 2; i++)
            await client.GetAsync("/Hello");

        var response = await client.GetAsync("/Hello");
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task XApiKey_UsedAsClientId()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-key-123");

        for (var i = 0; i < 2; i++)
            await client.GetAsync("/Hello");

        var response = await client.GetAsync("/Hello");
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task RemoteIp_UsedAsFallback()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        // No identity headers — falls back to remote IP

        for (var i = 0; i < 2; i++)
            await client.GetAsync("/Hello");

        var response = await client.GetAsync("/Hello");
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task DifferentApiKeys_TrackedSeparately()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        // Exhaust limit for key-1
        client.DefaultRequestHeaders.Add("X-Api-Key", "key-1");
        for (var i = 0; i < 2; i++)
            await client.GetAsync("/Hello");

        var rejectedKey1 = await client.GetAsync("/Hello");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedKey1.StatusCode);

        // key-2 should still be allowed
        client.DefaultRequestHeaders.Remove("X-Api-Key");
        client.DefaultRequestHeaders.Add("X-Api-Key", "key-2");
        var responseKey2 = await client.GetAsync("/Hello");
        Assert.Equal(HttpStatusCode.OK, responseKey2.StatusCode);
    }

    private RateLimiterWebApplicationFactory CreateFactory() =>
        new(_fixture.Connection, [TestRule]);
}
