namespace RateLimiter.Application.Interfaces;

public interface IRateLimitStore
{
    Task<long> IncrementCounter(string key, TimeSpan window);
    Task<long> GetCounter(string key);
    Task<long> AddToSortedSet(string key, double score, string member);
    Task<long> RemoveRangeByScore(string key, double min, double max);
    Task<long> CountSortedSet(string key);
    Task<string?> GetValue(string key);
    Task SetValue(string key, string value, TimeSpan expiry);
    Task SetExpiry(string key, TimeSpan expiry);
    Task<(long current, long previous)> GetSlidingWindowCounts(string currentKey, string previousKey);
}
