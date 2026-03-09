using RateLimiter.Domain.ValueObjects;

namespace RateLimiter.UnitTests.Domain;

public class RateLimitResultTests
{
    [Fact]
    public void IsAllowed_True_WhenRequestIsPermitted()
    {
        var result = new RateLimitResult(IsAllowed: true, Limit: 10, Remaining: 9, RetryAfterSeconds: 0);

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void IsAllowed_False_WhenRequestIsRejected()
    {
        var result = new RateLimitResult(IsAllowed: false, Limit: 10, Remaining: 0, RetryAfterSeconds: 30);

        Assert.False(result.IsAllowed);
    }

    [Fact]
    public void Properties_AreSetCorrectly()
    {
        var result = new RateLimitResult(IsAllowed: true, Limit: 100, Remaining: 42, RetryAfterSeconds: 0);

        Assert.Equal(100, result.Limit);
        Assert.Equal(42, result.Remaining);
        Assert.Equal(0, result.RetryAfterSeconds);
    }

    [Fact]
    public void EqualityByValue_WhenSameProperties()
    {
        var a = new RateLimitResult(IsAllowed: true, Limit: 10, Remaining: 5, RetryAfterSeconds: 0);
        var b = new RateLimitResult(IsAllowed: true, Limit: 10, Remaining: 5, RetryAfterSeconds: 0);

        Assert.Equal(a, b);
    }

    [Fact]
    public void NotEqual_WhenPropertiesDiffer()
    {
        var a = new RateLimitResult(IsAllowed: true,  Limit: 10, Remaining: 5, RetryAfterSeconds: 0);
        var b = new RateLimitResult(IsAllowed: false, Limit: 10, Remaining: 0, RetryAfterSeconds: 60);

        Assert.NotEqual(a, b);
    }
}
