using RateLimiter.Domain.Entities;

namespace RateLimiter.Application.Interfaces;

public interface IRuleProvider
{
    Task<RateLimitRule?> GetRule(string domain, string descriptor, string descriptorValue);
    Task<IReadOnlyList<RateLimitRule>> GetAllRules();
}
