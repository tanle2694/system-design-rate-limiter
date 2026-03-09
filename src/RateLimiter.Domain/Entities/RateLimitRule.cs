using RateLimiter.Domain.Enums;

namespace RateLimiter.Domain.Entities;

public sealed class RateLimitRule
{
    public string Domain { get; init; } = string.Empty;
    public string Descriptor { get; init; } = string.Empty;
    public string DescriptorValue { get; init; } = string.Empty;
    public TimeUnit Unit { get; init; }
    public int RequestsPerUnit { get; init; }
    public RateLimitAlgorithm Algorithm { get; init; }

    public TimeSpan GetWindowSize() => Unit switch
    {
        TimeUnit.Second => TimeSpan.FromSeconds(1),
        TimeUnit.Minute => TimeSpan.FromMinutes(1),
        TimeUnit.Hour   => TimeSpan.FromHours(1),
        TimeUnit.Day    => TimeSpan.FromDays(1),
        _ => throw new InvalidOperationException($"Unknown TimeUnit: {Unit}")
    };
}
