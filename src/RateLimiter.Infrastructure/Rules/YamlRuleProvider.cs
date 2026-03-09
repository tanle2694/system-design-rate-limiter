using RateLimiter.Application.Interfaces;
using RateLimiter.Domain.Entities;
using RateLimiter.Domain.Enums;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RateLimiter.Infrastructure.Rules;

public sealed class YamlRuleProvider : IRuleProvider
{
    private readonly string _filePath;
    private IReadOnlyList<RateLimitRule> _rules = [];
    private readonly SemaphoreSlim _lock = new(1, 1);
    private DateTime _lastLoaded = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    public YamlRuleProvider(string filePath)
    {
        _filePath = filePath;
    }

    public async Task<RateLimitRule?> GetRule(string domain, string descriptor, string descriptorValue)
    {
        var rules = await GetAllRules();
        return rules.FirstOrDefault(r =>
            string.Equals(r.Domain, domain, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.Descriptor, descriptor, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.DescriptorValue, descriptorValue, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<RateLimitRule>> GetAllRules()
    {
        if (DateTime.UtcNow - _lastLoaded < CacheDuration)
            return _rules;

        await _lock.WaitAsync();
        try
        {
            if (DateTime.UtcNow - _lastLoaded < CacheDuration)
                return _rules;

            _rules = LoadFromFile();
            _lastLoaded = DateTime.UtcNow;
        }
        finally
        {
            _lock.Release();
        }

        return _rules;
    }

    private IReadOnlyList<RateLimitRule> LoadFromFile()
    {
        var yaml = File.ReadAllText(_filePath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var config = deserializer.Deserialize<YamlRuleConfig>(yaml);
        return config.Descriptors
            .Select(d => MapToRule(config.Domain, d))
            .Where(r => r is not null)
            .Cast<RateLimitRule>()
            .ToList();
    }

    private static RateLimitRule? MapToRule(string domain, YamlDescriptor descriptor)
    {
        if (descriptor.RateLimit is null)
            return null;

        return new RateLimitRule
        {
            Domain = domain,
            Descriptor = descriptor.Key,
            DescriptorValue = descriptor.Value,
            Algorithm = ParseAlgorithm(descriptor.RateLimit.Algorithm),
            Unit = ParseTimeUnit(descriptor.RateLimit.Unit),
            RequestsPerUnit = descriptor.RateLimit.RequestsPerUnit
        };
    }

    private static RateLimitAlgorithm ParseAlgorithm(string value) => value.ToLowerInvariant() switch
    {
        "token_bucket" => RateLimitAlgorithm.TokenBucket,
        "fixed_window_counter" => RateLimitAlgorithm.FixedWindowCounter,
        "sliding_window_log" => RateLimitAlgorithm.SlidingWindowLog,
        "sliding_window_counter" => RateLimitAlgorithm.SlidingWindowCounter,
        _ => RateLimitAlgorithm.TokenBucket
    };

    private static TimeUnit ParseTimeUnit(string value) => value.ToLowerInvariant() switch
    {
        "second" => TimeUnit.Second,
        "minute" => TimeUnit.Minute,
        "hour" => TimeUnit.Hour,
        "day" => TimeUnit.Day,
        _ => TimeUnit.Minute
    };

    // --- YAML DTOs ---

    private sealed class YamlRuleConfig
    {
        public string Domain { get; set; } = string.Empty;
        public List<YamlDescriptor> Descriptors { get; set; } = [];
    }

    private sealed class YamlDescriptor
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public YamlRateLimit? RateLimit { get; set; }
    }

    private sealed class YamlRateLimit
    {
        public string Algorithm { get; set; } = "token_bucket";
        public string Unit { get; set; } = "minute";
        public int RequestsPerUnit { get; set; }
    }
}
