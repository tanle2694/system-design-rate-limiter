using RateLimiter.Application.Algorithms;
using RateLimiter.Domain.Entities;
using RateLimiter.Domain.Enums;
using RateLimiter.Infrastructure.Redis;
using RateLimiter.IntegrationTests.Fixtures;

namespace RateLimiter.IntegrationTests.Algorithms;

[Collection(RedisCollection.Name)]
public sealed class FixedWindowCounterAlgorithmIntegrationTests : IAsyncLifetime
{
    private readonly RedisFixture _fixture;
    private readonly FixedWindowCounterAlgorithm _algorithm;

    private readonly RateLimitRule _rule = new()
    {
        Domain = "api",
        Descriptor = "endpoint",
        DescriptorValue = "/test/fixed-window",
        Unit = TimeUnit.Second,
        RequestsPerUnit = 3,
        Algorithm = RateLimitAlgorithm.FixedWindowCounter,
    };

    public FixedWindowCounterAlgorithmIntegrationTests(RedisFixture fixture)
    {
        _fixture = fixture;
        var store = new RedisRateLimitStore(fixture.Connection);
        _algorithm = new FixedWindowCounterAlgorithm(store);
    }

    public Task InitializeAsync() => _fixture.FlushDb();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RequestsWithinLimit_AreAllowed()
    {
        var clientKey = "client:fw:allowed";

        for (var i = 0; i < 3; i++)
        {
            var result = await _algorithm.IsAllowed(clientKey, _rule);
            Assert.True(result.IsAllowed);
        }
    }

    [Fact]
    public async Task RequestBeyondLimit_IsRejected()
    {
        var clientKey = "client:fw:rejected";

        for (var i = 0; i < 3; i++)
            await _algorithm.IsAllowed(clientKey, _rule);

        var result = await _algorithm.IsAllowed(clientKey, _rule);
        Assert.False(result.IsAllowed);
        Assert.Equal(0, result.Remaining);
    }

    [Fact]
    public async Task RemainingCount_DecrementsCorrectly()
    {
        var clientKey = "client:fw:remaining";

        var r1 = await _algorithm.IsAllowed(clientKey, _rule);
        Assert.Equal(2, r1.Remaining);

        var r2 = await _algorithm.IsAllowed(clientKey, _rule);
        Assert.Equal(1, r2.Remaining);

        var r3 = await _algorithm.IsAllowed(clientKey, _rule);
        Assert.Equal(0, r3.Remaining);
    }

    [Fact]
    public async Task NewWindow_ResetsCounter()
    {
        var clientKey = "client:fw:reset";

        for (var i = 0; i < 3; i++)
            await _algorithm.IsAllowed(clientKey, _rule);

        var rejected = await _algorithm.IsAllowed(clientKey, _rule);
        Assert.False(rejected.IsAllowed);

        // Wait for the next 1-second window
        await Task.Delay(1200);

        var result = await _algorithm.IsAllowed(clientKey, _rule);
        Assert.True(result.IsAllowed);
    }
}
