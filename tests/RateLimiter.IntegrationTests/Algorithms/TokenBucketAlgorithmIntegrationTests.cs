using RateLimiter.Application.Algorithms;
using RateLimiter.Domain.Entities;
using RateLimiter.Domain.Enums;
using RateLimiter.Infrastructure.Redis;
using RateLimiter.IntegrationTests.Fixtures;

namespace RateLimiter.IntegrationTests.Algorithms;

[Collection(RedisCollection.Name)]
public sealed class TokenBucketAlgorithmIntegrationTests : IAsyncLifetime
{
    private readonly RedisFixture _fixture;
    private readonly TokenBucketAlgorithm _algorithm;

    private readonly RateLimitRule _rule = new()
    {
        Domain = "api",
        Descriptor = "endpoint",
        DescriptorValue = "/test/token-bucket",
        Unit = TimeUnit.Second,
        RequestsPerUnit = 5,
        Algorithm = RateLimitAlgorithm.TokenBucket,
    };

    public TokenBucketAlgorithmIntegrationTests(RedisFixture fixture)
    {
        _fixture = fixture;
        var store = new RedisRateLimitStore(fixture.Connection);
        _algorithm = new TokenBucketAlgorithm(store);
    }

    public Task InitializeAsync() => _fixture.FlushDb();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task FirstRequest_IsAllowed_WithFullBucket()
    {
        var result = await _algorithm.IsAllowed("client:tb:first", _rule);

        Assert.True(result.IsAllowed);
        Assert.Equal(5, result.Limit);
        Assert.Equal(4, result.Remaining);
    }

    [Fact]
    public async Task RequestsUpToLimit_AreAllAllowed()
    {
        var clientKey = "client:tb:limit";

        for (var i = 0; i < 5; i++)
        {
            var result = await _algorithm.IsAllowed(clientKey, _rule);
            Assert.True(result.IsAllowed);
        }
    }

    [Fact]
    public async Task RequestBeyondLimit_IsRejected()
    {
        var clientKey = "client:tb:rejected";

        for (var i = 0; i < 5; i++)
            await _algorithm.IsAllowed(clientKey, _rule);

        var result = await _algorithm.IsAllowed(clientKey, _rule);
        Assert.False(result.IsAllowed);
        Assert.Equal(0, result.Remaining);
    }

    [Fact]
    public async Task RejectedRequest_HasPositiveRetryAfter()
    {
        var clientKey = "client:tb:retry";

        for (var i = 0; i < 5; i++)
            await _algorithm.IsAllowed(clientKey, _rule);

        var result = await _algorithm.IsAllowed(clientKey, _rule);
        Assert.False(result.IsAllowed);
        Assert.True(result.RetryAfterSeconds > 0);
    }

    [Fact]
    public async Task TokensRefill_AfterWaiting()
    {
        var clientKey = "client:tb:refill";

        for (var i = 0; i < 5; i++)
            await _algorithm.IsAllowed(clientKey, _rule);

        var rejected = await _algorithm.IsAllowed(clientKey, _rule);
        Assert.False(rejected.IsAllowed);

        // Wait for tokens to refill (1 second = full refill for 5 req/sec)
        await Task.Delay(1200);

        var result = await _algorithm.IsAllowed(clientKey, _rule);
        Assert.True(result.IsAllowed);
    }
}
