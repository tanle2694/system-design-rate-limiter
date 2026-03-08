using RateLimiter.Domain.Entities;
using RateLimiter.Domain.ValueObjects;

namespace RateLimiter.Domain.Algorithms;

public interface IRateLimitAlgorithm
{
    Task<RateLimitResult> IsAllowed(string clientKey, RateLimitRule rule);
}
