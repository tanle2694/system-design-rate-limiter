namespace RateLimiter.Domain.ValueObjects;

public sealed record RateLimitResult(
    bool IsAllowed,
    int Limit,
    int Remaining,
    long RetryAfterSeconds);
