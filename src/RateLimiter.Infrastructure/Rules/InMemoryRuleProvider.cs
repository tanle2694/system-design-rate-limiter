using RateLimiter.Application.Interfaces;
using RateLimiter.Domain.Entities;

namespace RateLimiter.Infrastructure.Rules;

public sealed class InMemoryRuleProvider : IRuleProvider
{
    private readonly IReadOnlyList<RateLimitRule> _rules;

    public InMemoryRuleProvider(IEnumerable<RateLimitRule> rules)
    {
        _rules = rules.ToList();
    }

    public Task<RateLimitRule?> GetRule(string domain, string descriptor, string descriptorValue)
    {
        var rule = _rules.FirstOrDefault(r =>
            string.Equals(r.Domain, domain, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.Descriptor, descriptor, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.DescriptorValue, descriptorValue, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(rule);
    }

    public Task<IReadOnlyList<RateLimitRule>> GetAllRules()
    {
        return Task.FromResult(_rules);
    }
}
