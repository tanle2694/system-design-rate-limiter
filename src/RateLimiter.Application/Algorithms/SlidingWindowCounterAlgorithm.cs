using RateLimiter.Application.Interfaces;
using RateLimiter.Domain.Algorithms;
using RateLimiter.Domain.Entities;
using RateLimiter.Domain.ValueObjects;

namespace RateLimiter.Application.Algorithms;

public sealed class SlidingWindowCounterAlgorithm : IRateLimitAlgorithm
{
    private readonly IRateLimitStore _store;

    public SlidingWindowCounterAlgorithm(IRateLimitStore store)
    {
        _store = store;
    }

    public async Task<RateLimitResult> IsAllowed(string clientKey, RateLimitRule rule)
    {
        var window = rule.GetWindowSize();
        var limit = rule.RequestsPerUnit;
        var now = DateTimeOffset.UtcNow;
        var windowSizeSeconds = (long)window.TotalSeconds;

        var currentWindowId = now.ToUnixTimeSeconds() / windowSizeSeconds;
        var prevWindowId = currentWindowId - 1;

        var prefix = $"rl:swc:{clientKey}:{rule.Domain}:{rule.Descriptor}:{rule.DescriptorValue}";
        var currentKey = $"{prefix}:{currentWindowId}";
        var prevKey = $"{prefix}:{prevWindowId}";

        var currentCount = await _store.GetCounter(currentKey);
        var prevCount = await _store.GetCounter(prevKey);

        // Weight the previous window by how much of it overlaps with the current window
        var secondsIntoCurrentWindow = now.ToUnixTimeSeconds() % windowSizeSeconds;
        var overlapPercentage = 1.0 - (double)secondsIntoCurrentWindow / windowSizeSeconds;
        var weightedCount = currentCount + prevCount * overlapPercentage;

        if (weightedCount >= limit)
        {
            var retryAfter = windowSizeSeconds - secondsIntoCurrentWindow;
            return new RateLimitResult(false, limit, 0, retryAfter);
        }

        var newCount = await _store.IncrementCounter(currentKey, window * 2);
        var remaining = Math.Max(0, limit - (int)newCount);
        return new RateLimitResult(true, limit, remaining, 0);
    }
}
