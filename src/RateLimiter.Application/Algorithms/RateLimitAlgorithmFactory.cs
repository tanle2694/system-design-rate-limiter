using RateLimiter.Application.Interfaces;
using RateLimiter.Domain.Algorithms;
using RateLimiter.Domain.Enums;

namespace RateLimiter.Application.Algorithms;

public sealed class RateLimitAlgorithmFactory
{
    private readonly IRateLimitStore _store;

    public RateLimitAlgorithmFactory(IRateLimitStore store)
    {
        _store = store;
    }

    public IRateLimitAlgorithm Create(RateLimitAlgorithm algorithm) => algorithm switch
    {
        RateLimitAlgorithm.TokenBucket => new TokenBucketAlgorithm(_store),
        RateLimitAlgorithm.FixedWindowCounter => new FixedWindowCounterAlgorithm(_store),
        RateLimitAlgorithm.SlidingWindowLog => new SlidingWindowLogAlgorithm(_store),
        RateLimitAlgorithm.SlidingWindowCounter => new SlidingWindowCounterAlgorithm(_store),
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm), $"Unknown algorithm: {algorithm}")
    };
}
