using RateLimiter.Application.Interfaces;
using RateLimiter.Domain.Algorithms;
using RateLimiter.Domain.Entities;
using RateLimiter.Domain.ValueObjects;

namespace RateLimiter.Application.Algorithms;

public sealed class SlidingWindowLogAlgorithm : IRateLimitAlgorithm
{
    private readonly IRateLimitStore _store;

    public SlidingWindowLogAlgorithm(IRateLimitStore store)
    {
        _store = store;
    }

    public async Task<RateLimitResult> IsAllowed(string clientKey, RateLimitRule rule)
    {
        var key = $"rl:swl:{clientKey}:{rule.Domain}:{rule.Descriptor}:{rule.DescriptorValue}";
        var window = rule.GetWindowSize();
        var limit = rule.RequestsPerUnit;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var cutoff = now - (long)window.TotalMilliseconds;

        // Remove entries outside the sliding window
        await _store.RemoveRangeByScore(key, 0, cutoff);

        // Count entries currently in the window
        var count = await _store.CountSortedSet(key);

        if (count >= limit)
            return new RateLimitResult(false, limit, 0, (long)Math.Ceiling(window.TotalSeconds));

        // Add this request to the log
        var member = $"{now}:{Guid.NewGuid()}";
        await _store.AddToSortedSet(key, now, member);
        await _store.SetExpiry(key, window);

        var remaining = Math.Max(0, limit - (int)(count + 1));
        return new RateLimitResult(true, limit, remaining, 0);
    }
}
