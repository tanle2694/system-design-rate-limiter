using RateLimiter.Application.Interfaces;
using RateLimiter.Domain.Algorithms;
using RateLimiter.Domain.Entities;
using RateLimiter.Domain.ValueObjects;

namespace RateLimiter.Application.Algorithms;

public sealed class TokenBucketAlgorithm : IRateLimitAlgorithm
{
    private readonly IRateLimitStore _store;

    public TokenBucketAlgorithm(IRateLimitStore store)
    {
        _store = store;
    }

    public async Task<RateLimitResult> IsAllowed(string clientKey, RateLimitRule rule)
    {
        var key = $"rl:tb:{{{clientKey}:{rule.Domain}:{rule.Descriptor}:{rule.DescriptorValue}}}";
        var bucketCapacity = rule.RequestsPerUnit;
        var refillRate = (double)rule.RequestsPerUnit / rule.GetWindowSize().TotalSeconds;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var stored = await _store.GetValue(key);

        double tokens;
        long lastRefill;

        if (stored is null)
        {
            tokens = bucketCapacity;
            lastRefill = now;
        }
        else
        {
            var parts = stored.Split(':');
            tokens = double.Parse(parts[0]);
            lastRefill = long.Parse(parts[1]);
        }

        // Refill tokens proportional to elapsed time
        var elapsedSeconds = (now - lastRefill) / 1000.0;
        tokens = Math.Min(bucketCapacity, tokens + elapsedSeconds * refillRate);

        var expiry = rule.GetWindowSize() * 2;

        if (tokens >= 1)
        {
            tokens -= 1;
            await _store.SetValue(key, $"{tokens:F6}:{now}", expiry);
            return new RateLimitResult(true, bucketCapacity, (int)Math.Floor(tokens), 0);
        }

        // Time until 1 token is available
        var retryAfterSeconds = (long)Math.Ceiling((1 - tokens) / refillRate);
        await _store.SetValue(key, $"{tokens:F6}:{now}", expiry);
        return new RateLimitResult(false, bucketCapacity, 0, retryAfterSeconds);
    }
}
