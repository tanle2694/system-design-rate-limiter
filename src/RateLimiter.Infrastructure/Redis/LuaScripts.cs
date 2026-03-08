namespace RateLimiter.Infrastructure.Redis;

internal static class LuaScripts
{
    /// <summary>
    /// Atomically increments a counter and sets expiry on first creation.
    /// KEYS[1] = key, ARGV[1] = TTL in seconds
    /// Returns the new counter value.
    /// </summary>
    public const string IncrementWithExpiry = """
        local current = redis.call('INCR', KEYS[1])
        if current == 1 then
            redis.call('EXPIRE', KEYS[1], ARGV[1])
        end
        return current
        """;

    /// <summary>
    /// Atomically retrieves the two fixed-window counters used by the sliding window counter algorithm.
    /// KEYS[1] = current window key, KEYS[2] = previous window key
    /// Returns { currentCount, previousCount }
    /// </summary>
    public const string GetSlidingWindowCounts = """
        local current = redis.call('GET', KEYS[1])
        local previous = redis.call('GET', KEYS[2])
        return { current or 0, previous or 0 }
        """;
}
