using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RateLimiter.Application.Interfaces;
using RateLimiter.Domain.Entities;
using RateLimiter.Infrastructure.Configuration;
using StackExchange.Redis;

namespace RateLimiter.IntegrationTests.Fixtures;

public sealed class RateLimiterWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IReadOnlyList<RateLimitRule> _rules;
    private readonly Action<RateLimiterOptions>? _configureOptions;

    public RateLimiterWebApplicationFactory(
        IConnectionMultiplexer redis,
        IReadOnlyList<RateLimitRule> rules,
        Action<RateLimiterOptions>? configureOptions = null)
    {
        _redis = redis;
        _rules = rules;
        _configureOptions = configureOptions;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace Redis connection
            var redisDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IConnectionMultiplexer));
            if (redisDescriptor is not null)
                services.Remove(redisDescriptor);
            services.AddSingleton(_redis);

            // Replace rule provider
            var ruleProviderDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IRuleProvider));
            if (ruleProviderDescriptor is not null)
                services.Remove(ruleProviderDescriptor);
            services.AddSingleton<IRuleProvider>(new InMemoryRuleProvider(_rules));

            // Configure options
            if (_configureOptions is not null)
            {
                services.PostConfigure(_configureOptions);
            }
        });
    }
}

internal sealed class InMemoryRuleProvider : IRuleProvider
{
    private readonly IReadOnlyList<RateLimitRule> _rules;

    public InMemoryRuleProvider(IReadOnlyList<RateLimitRule> rules)
    {
        _rules = rules;
    }

    public Task<RateLimitRule?> GetRule(string domain, string descriptor, string descriptorValue)
    {
        var rule = _rules.FirstOrDefault(r =>
            r.Domain == domain &&
            r.Descriptor == descriptor &&
            r.DescriptorValue == descriptorValue);
        return Task.FromResult(rule);
    }

    public Task<IReadOnlyList<RateLimitRule>> GetAllRules() => Task.FromResult(_rules);
}
