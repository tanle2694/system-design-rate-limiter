using RateLimiter.Application.Interfaces;
using RateLimiter.Domain.Algorithms;
using RateLimiter.Domain.Entities;
using RateLimiter.Domain.ValueObjects;

namespace RateLimiter.Application.Algorithms;

public sealed class FixedWindowCounterAlgorithm : IRateLimitAlgorithm
{
    private readonly IRateLimitStore _store;

    public FixedWindowCounterAlgorithm(IRateLimitStore store)
    {
        _store = store;
    }

    public async Task<RateLimitResult> IsAllowed(string clientKey, RateLimitRule rule)
    {
        var window = rule.GetWindowSize();
        var windowSizeSeconds = (long)window.TotalSeconds;
        var windowId = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / windowSizeSeconds;
        var key = $"rl:fw:{{{clientKey}:{rule.Domain}:{rule.Descriptor}:{rule.DescriptorValue}}}:{windowId}";

        var count = await _store.IncrementCounter(key, window);
        var limit = rule.RequestsPerUnit;
        var remaining = Math.Max(0, limit - (int)count);

        if (count <= limit)
            return new RateLimitResult(true, limit, remaining, 0);

        var windowEnd = (windowId + 1) * windowSizeSeconds;
        var retryAfter = windowEnd - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return new RateLimitResult(false, limit, 0, Math.Max(0, retryAfter));
    }
}
