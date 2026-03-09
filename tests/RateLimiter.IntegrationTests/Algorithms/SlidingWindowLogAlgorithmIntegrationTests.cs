using RateLimiter.Application.Algorithms;
using RateLimiter.Domain.Entities;
using RateLimiter.Domain.Enums;
using RateLimiter.Infrastructure.Redis;
using RateLimiter.IntegrationTests.Fixtures;

namespace RateLimiter.IntegrationTests.Algorithms;

[Collection(RedisCollection.Name)]
public sealed class SlidingWindowLogAlgorithmIntegrationTests : IAsyncLifetime
{
    private readonly RedisFixture _fixture;
    private readonly SlidingWindowLogAlgorithm _algorithm;

    private readonly RateLimitRule _rule = new()
    {
        Domain = "api",
        Descriptor = "endpoint",
        DescriptorValue = "/test/sliding-log",
        Unit = TimeUnit.Second,
        RequestsPerUnit = 3,
        Algorithm = RateLimitAlgorithm.SlidingWindowLog,
    };

    public SlidingWindowLogAlgorithmIntegrationTests(RedisFixture fixture)
    {
        _fixture = fixture;
        var store = new RedisRateLimitStore(fixture.Connection);
        _algorithm = new SlidingWindowLogAlgorithm(store);
    }

    public Task InitializeAsync() => _fixture.FlushDb();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RequestsWithinLimit_AreAllowed()
    {
        var clientKey = "client:swl:allowed";

        for (var i = 0; i < 3; i++)
        {
            var result = await _algorithm.IsAllowed(clientKey, _rule);
            Assert.True(result.IsAllowed);
        }
    }

    [Fact]
    public async Task RequestBeyondLimit_IsRejected()
    {
        var clientKey = "client:swl:rejected";

        for (var i = 0; i < 3; i++)
            await _algorithm.IsAllowed(clientKey, _rule);

        var result = await _algorithm.IsAllowed(clientKey, _rule);
        Assert.False(result.IsAllowed);
        Assert.Equal(0, result.Remaining);
    }

    [Fact]
    public async Task OldEntries_AreExpiredFromWindow()
    {
        var clientKey = "client:swl:expiry";

        for (var i = 0; i < 3; i++)
            await _algorithm.IsAllowed(clientKey, _rule);

        var rejected = await _algorithm.IsAllowed(clientKey, _rule);
        Assert.False(rejected.IsAllowed);

        // Wait for entries to expire from the 1-second window
        await Task.Delay(1200);

        var result = await _algorithm.IsAllowed(clientKey, _rule);
        Assert.True(result.IsAllowed);
    }
}
