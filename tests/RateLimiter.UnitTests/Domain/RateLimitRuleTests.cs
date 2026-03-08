using RateLimiter.Domain.Entities;
using RateLimiter.Domain.Enums;

namespace RateLimiter.UnitTests.Domain;

public class RateLimitRuleTests
{
    [Theory]
    [InlineData(TimeUnit.Second, 1)]
    [InlineData(TimeUnit.Minute, 60)]
    [InlineData(TimeUnit.Hour,   3600)]
    [InlineData(TimeUnit.Day,    86400)]
    public void GetWindowSize_ReturnsCorrectSeconds(TimeUnit unit, int expectedSeconds)
    {
        var rule = new RateLimitRule { Unit = unit };

        var window = rule.GetWindowSize();

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), window);
    }

    [Fact]
    public void Properties_AreSetCorrectly()
    {
        var rule = new RateLimitRule
        {
            Domain          = "api",
            Descriptor      = "endpoint",
            DescriptorValue = "/hello",
            Unit            = TimeUnit.Minute,
            RequestsPerUnit = 100,
            Algorithm       = RateLimitAlgorithm.TokenBucket
        };

        Assert.Equal("api",              rule.Domain);
        Assert.Equal("endpoint",         rule.Descriptor);
        Assert.Equal("/hello",           rule.DescriptorValue);
        Assert.Equal(TimeUnit.Minute,    rule.Unit);
        Assert.Equal(100,                rule.RequestsPerUnit);
        Assert.Equal(RateLimitAlgorithm.TokenBucket, rule.Algorithm);
    }

    [Fact]
    public void DefaultValues_AreEmpty()
    {
        var rule = new RateLimitRule();

        Assert.Equal(string.Empty, rule.Domain);
        Assert.Equal(string.Empty, rule.Descriptor);
        Assert.Equal(string.Empty, rule.DescriptorValue);
    }
}
