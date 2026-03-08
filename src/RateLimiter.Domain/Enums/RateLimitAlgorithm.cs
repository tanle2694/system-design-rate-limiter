namespace RateLimiter.Domain.Enums;

public enum RateLimitAlgorithm
{
    TokenBucket,
    SlidingWindowCounter,
    FixedWindowCounter,
    SlidingWindowLog
}
