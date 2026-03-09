using RateLimiter.Domain.Enums;

namespace RateLimiter.Api.Attributes;

/// <summary>
/// Marks a controller or action with per-endpoint rate limit overrides.
/// The middleware reads this attribute from endpoint metadata to apply custom limits.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RateLimitAttribute : Attribute
{
    public int Limit { get; set; } = 100;
    public int WindowSeconds { get; set; } = 60;
    public RateLimitAlgorithm Algorithm { get; set; } = RateLimitAlgorithm.TokenBucket;
}
