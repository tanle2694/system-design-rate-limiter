using RateLimiter.Application.Interfaces;
using StackExchange.Redis;

namespace RateLimiter.Infrastructure.Redis;

public sealed class RedisRateLimitStore : IRateLimitStore
{
    private readonly IConnectionMultiplexer _redis;

    public RedisRateLimitStore(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    private IDatabase Db => _redis.GetDatabase();

    public async Task<long> IncrementCounter(string key, TimeSpan window)
    {
        var result = await Db.ScriptEvaluateAsync(
            LuaScripts.IncrementWithExpiry,
            keys: [key],
            values: [(int)window.TotalSeconds]);

        return (long)result;
    }

    public async Task<long> GetCounter(string key)
    {
        var value = await Db.StringGetAsync(key);
        return value.HasValue && long.TryParse(value, out var count) ? count : 0;
    }

    public async Task<long> AddToSortedSet(string key, double score, string member)
    {
        await Db.SortedSetAddAsync(key, member, score);
        return await Db.SortedSetLengthAsync(key);
    }

    public async Task<long> RemoveRangeByScore(string key, double min, double max)
    {
        return await Db.SortedSetRemoveRangeByScoreAsync(key, min, max);
    }

    public async Task<long> CountSortedSet(string key)
    {
        return await Db.SortedSetLengthAsync(key);
    }

    public async Task<string?> GetValue(string key)
    {
        var value = await Db.StringGetAsync(key);
        return value.HasValue ? value.ToString() : null;
    }

    public async Task SetValue(string key, string value, TimeSpan expiry)
    {
        await Db.StringSetAsync(key, value, expiry);
    }

    public async Task SetExpiry(string key, TimeSpan expiry)
    {
        await Db.KeyExpireAsync(key, expiry);
    }

    public async Task<(long current, long previous)> GetSlidingWindowCounts(string currentKey, string previousKey)
    {
        var results = (RedisResult[]?)await Db.ScriptEvaluateAsync(
            LuaScripts.GetSlidingWindowCounts,
            keys: [currentKey, previousKey]);

        if (results is null || results.Length < 2)
            return (0, 0);

        var current = (long)results[0];
        var previous = (long)results[1];
        return (current, previous);
    }
}
