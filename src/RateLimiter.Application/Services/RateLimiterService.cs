using RateLimiter.Application.Algorithms;
using RateLimiter.Application.Interfaces;
using RateLimiter.Domain.ValueObjects;

namespace RateLimiter.Application.Services;

public sealed class RateLimiterService
{
    private readonly IRuleProvider _ruleProvider;
    private readonly RateLimitAlgorithmFactory _algorithmFactory;

    private const string Domain = "api";
    private const string Descriptor = "endpoint";

    public RateLimiterService(IRuleProvider ruleProvider, RateLimitAlgorithmFactory algorithmFactory)
    {
        _ruleProvider = ruleProvider;
        _algorithmFactory = algorithmFactory;
    }

    public async Task<RateLimitResult> CheckRateLimit(string clientKey, string endpoint)
    {
        var rule = await _ruleProvider.GetRule(Domain, Descriptor, endpoint);
        if (rule is null)
            return new RateLimitResult(true, int.MaxValue, int.MaxValue, 0);

        var algorithm = _algorithmFactory.Create(rule.Algorithm);
        return await algorithm.IsAllowed(clientKey, rule);
    }
}
