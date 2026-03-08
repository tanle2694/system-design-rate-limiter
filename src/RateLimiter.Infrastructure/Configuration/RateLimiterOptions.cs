using RateLimiter.Domain.Enums;

namespace RateLimiter.Infrastructure.Configuration;

public sealed class RateLimiterOptions
{
    public const string SectionName = "RateLimiter";

    public bool Enabled { get; set; } = true;
    public RateLimitAlgorithm DefaultAlgorithm { get; set; } = RateLimitAlgorithm.TokenBucket;
    public int DefaultLimit { get; set; } = 100;
    public int DefaultWindowSeconds { get; set; } = 60;
    public bool EnableHeaders { get; set; } = true;
}
