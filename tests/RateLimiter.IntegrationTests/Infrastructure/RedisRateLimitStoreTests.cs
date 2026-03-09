using RateLimiter.Infrastructure.Redis;
using RateLimiter.IntegrationTests.Fixtures;

namespace RateLimiter.IntegrationTests.Infrastructure;

[Collection(RedisCollection.Name)]
public sealed class RedisRateLimitStoreTests : IAsyncLifetime
{
    private readonly RedisFixture _fixture;
    private readonly RedisRateLimitStore _store;

    public RedisRateLimitStoreTests(RedisFixture fixture)
    {
        _fixture = fixture;
        _store = new RedisRateLimitStore(fixture.Connection);
    }

    public Task InitializeAsync() => _fixture.FlushDb();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task IncrementCounter_FirstCall_ReturnsOne()
    {
        var result = await _store.IncrementCounter("test:incr:first", TimeSpan.FromSeconds(10));
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task IncrementCounter_MultipleCalls_IncrementsCorrectly()
    {
        var key = "test:incr:multi";
        await _store.IncrementCounter(key, TimeSpan.FromSeconds(10));
        await _store.IncrementCounter(key, TimeSpan.FromSeconds(10));
        var result = await _store.IncrementCounter(key, TimeSpan.FromSeconds(10));

        Assert.Equal(3, result);
    }

    [Fact]
    public async Task IncrementCounter_SetsExpiry_OnFirstCallOnly()
    {
        var key = "test:incr:expiry";
        await _store.IncrementCounter(key, TimeSpan.FromSeconds(30));

        var db = _fixture.Connection.GetDatabase();
        var ttl = await db.KeyTimeToLiveAsync(key);

        Assert.NotNull(ttl);
        Assert.True(ttl.Value.TotalSeconds > 0 && ttl.Value.TotalSeconds <= 30);
    }

    [Fact]
    public async Task GetCounter_NonExistentKey_ReturnsZero()
    {
        var result = await _store.GetCounter("test:counter:nonexistent");
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetCounter_ExistingKey_ReturnsValue()
    {
        var key = "test:counter:existing";
        await _store.IncrementCounter(key, TimeSpan.FromSeconds(10));
        await _store.IncrementCounter(key, TimeSpan.FromSeconds(10));

        var result = await _store.GetCounter(key);
        Assert.Equal(2, result);
    }

    [Fact]
    public async Task AddToSortedSet_And_CountSortedSet_WorkCorrectly()
    {
        var key = "test:sortedset:basic";
        await _store.AddToSortedSet(key, 1.0, "member1");
        await _store.AddToSortedSet(key, 2.0, "member2");
        await _store.AddToSortedSet(key, 3.0, "member3");

        var count = await _store.CountSortedSet(key);
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task RemoveRangeByScore_RemovesExpiredEntries()
    {
        var key = "test:sortedset:remove";
        await _store.AddToSortedSet(key, 100, "old1");
        await _store.AddToSortedSet(key, 200, "old2");
        await _store.AddToSortedSet(key, 500, "current");

        var removed = await _store.RemoveRangeByScore(key, 0, 300);
        Assert.Equal(2, removed);

        var remaining = await _store.CountSortedSet(key);
        Assert.Equal(1, remaining);
    }

    [Fact]
    public async Task GetValue_SetValue_RoundTrip()
    {
        var key = "test:string:roundtrip";
        await _store.SetValue(key, "hello:world", TimeSpan.FromSeconds(10));

        var result = await _store.GetValue(key);
        Assert.Equal("hello:world", result);
    }

    [Fact]
    public async Task GetValue_NonExistentKey_ReturnsNull()
    {
        var result = await _store.GetValue("test:string:nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task SetExpiry_SetsKeyTTL()
    {
        var key = "test:expiry:set";
        var db = _fixture.Connection.GetDatabase();
        await db.SortedSetAddAsync(key, "member", 1.0);

        await _store.SetExpiry(key, TimeSpan.FromSeconds(30));

        var ttl = await db.KeyTimeToLiveAsync(key);
        Assert.NotNull(ttl);
        Assert.True(ttl.Value.TotalSeconds > 0 && ttl.Value.TotalSeconds <= 30);
    }

    [Fact]
    public async Task GetSlidingWindowCounts_BothKeysExist_ReturnsBoth()
    {
        var currentKey = "test:swc:current";
        var previousKey = "test:swc:previous";

        await _store.IncrementCounter(currentKey, TimeSpan.FromSeconds(10));
        await _store.IncrementCounter(currentKey, TimeSpan.FromSeconds(10));
        await _store.IncrementCounter(previousKey, TimeSpan.FromSeconds(10));
        await _store.IncrementCounter(previousKey, TimeSpan.FromSeconds(10));
        await _store.IncrementCounter(previousKey, TimeSpan.FromSeconds(10));

        var (current, previous) = await _store.GetSlidingWindowCounts(currentKey, previousKey);
        Assert.Equal(2, current);
        Assert.Equal(3, previous);
    }

    [Fact]
    public async Task GetSlidingWindowCounts_MissingKeys_ReturnsZeros()
    {
        var (current, previous) = await _store.GetSlidingWindowCounts(
            "test:swc:missing1", "test:swc:missing2");
        Assert.Equal(0, current);
        Assert.Equal(0, previous);
    }

    [Fact]
    public async Task ConcurrentIncrements_AreAtomic()
    {
        var key = "test:incr:concurrent";
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => _store.IncrementCounter(key, TimeSpan.FromSeconds(30)))
            .ToList();

        await Task.WhenAll(tasks);

        var finalCount = await _store.GetCounter(key);
        Assert.Equal(100, finalCount);
    }
}
