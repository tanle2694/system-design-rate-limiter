namespace RateLimiter.Infrastructure.Configuration;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string[] Endpoints { get; set; } = ["localhost:6379"];
    public string InstanceName { get; set; } = "rate-limiter";
    public string KeyPrefix { get; set; } = "rl:";
    public string? Password { get; set; }
    public int ConnectTimeout { get; set; } = 5000;
    public int SyncTimeout { get; set; } = 5000;
}
