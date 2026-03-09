using RateLimiter.Api.Middleware;
using RateLimiter.Api.Services;
using RateLimiter.Application.Algorithms;
using RateLimiter.Application.Interfaces;
using RateLimiter.Application.Services;
using RateLimiter.Infrastructure.Configuration;
using RateLimiter.Infrastructure.Redis;
using RateLimiter.Infrastructure.Rules;
using StackExchange.Redis;

namespace RateLimiter.Api.Extensions;

public static class RateLimiterServiceExtensions
{
    public static IServiceCollection AddRateLimiter(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind configuration sections
        services.Configure<RateLimiterOptions>(configuration.GetSection(RateLimiterOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));

        // Redis Cluster connection (singleton — connection multiplexer is thread-safe and expensive to create)
        var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var configOptions = new ConfigurationOptions
            {
                ConnectTimeout = redisOptions.ConnectTimeout,
                SyncTimeout = redisOptions.SyncTimeout,
                Password = redisOptions.Password,
            };

            foreach (var endpoint in redisOptions.Endpoints)
                configOptions.EndPoints.Add(endpoint);

            return ConnectionMultiplexer.Connect(configOptions);
        });

        // Infrastructure
        services.AddSingleton<IRateLimitStore, RedisRateLimitStore>();
        services.AddSingleton<IRuleProvider>(_ =>
        {
            var rulesPath = configuration["RateLimiter:RulesFile"]
                ?? Path.Combine(AppContext.BaseDirectory, "rules", "rate-limit-rules.yaml");
            return new YamlRuleProvider(rulesPath);
        });

        // Application
        services.AddSingleton<RateLimitAlgorithmFactory>();
        services.AddScoped<RateLimiterService>();

        // API
        services.AddSingleton<IClientIdentifier, HttpContextClientIdentifier>();

        return services;
    }

    public static IApplicationBuilder UseCustomRateLimiter(this IApplicationBuilder app)
    {
        app.UseMiddleware<RateLimiterMiddleware>();
        return app;
    }
}
