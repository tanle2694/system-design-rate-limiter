using RateLimiter.Application.Algorithms;
using RateLimiter.Domain.Entities;
using RateLimiter.Domain.Enums;
using RateLimiter.Infrastructure.Redis;
using RateLimiter.IntegrationTests.Fixtures;

namespace RateLimiter.IntegrationTests.Algorithms;

[Collection(RedisCollection.Name)]
public sealed class SlidingWindowCounterAlgorithmIntegrationTests : IAsyncLifetime
{
    private readonly RedisFixture _fixture;
    private readonly SlidingWindowCounterAlgorithm _algorithm;

    private readonly RateLimitRule _rule = new()
    {
        Domain = "api",
        Descriptor = "endpoint",
        DescriptorValue = "/test/sliding-counter",
        Unit = TimeUnit.Second,
        RequestsPerUnit = 5,
        Algorithm = RateLimitAlgorithm.SlidingWindowCounter,
    };

    public SlidingWindowCounterAlgorithmIntegrationTests(RedisFixture fixture)
    {
        _fixture = fixture;
        var store = new RedisRateLimitStore(fixture.Connection);
        _algorithm = new SlidingWindowCounterAlgorithm(store);
    }

    public Task InitializeAsync() => _fixture.FlushDb();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RequestsWithinLimit_AreAllowed()
    {
        var clientKey = "client:swc:allowed";

        for (var i = 0; i < 5; i++)
        {
            var result = await _algorithm.IsAllowed(clientKey, _rule);
            Assert.True(result.IsAllowed);
        }
    }

    [Fact]
    public async Task RequestBeyondLimit_IsRejected()
    {
        var clientKey = "client:swc:rejected";

        for (var i = 0; i < 5; i++)
            await _algorithm.IsAllowed(clientKey, _rule);

        var result = await _algorithm.IsAllowed(clientKey, _rule);
        Assert.False(result.IsAllowed);
        Assert.Equal(0, result.Remaining);
    }

    [Fact]
    public async Task CounterResetsAfterWindow()
    {
        var clientKey = "client:swc:reset";

        for (var i = 0; i < 5; i++)
            await _algorithm.IsAllowed(clientKey, _rule);

        var rejected = await _algorithm.IsAllowed(clientKey, _rule);
        Assert.False(rejected.IsAllowed);

        // Wait for the window to pass so previous window weight drops
        await Task.Delay(2200);

        var result = await _algorithm.IsAllowed(clientKey, _rule);
        Assert.True(result.IsAllowed);
    }
}
