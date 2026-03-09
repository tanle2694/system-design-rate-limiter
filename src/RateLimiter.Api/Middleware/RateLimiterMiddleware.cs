using RateLimiter.Application.Interfaces;
using RateLimiter.Application.Services;
using RateLimiter.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace RateLimiter.Api.Middleware;

public sealed class RateLimiterMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimiterMiddleware> _logger;
    private readonly RateLimiterOptions _options;

    public RateLimiterMiddleware(RequestDelegate next, ILogger<RateLimiterMiddleware> logger, IOptions<RateLimiterOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context, RateLimiterService rateLimiterService, IClientIdentifier clientIdentifier)
    {
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        var clientId = clientIdentifier.GetClientId(context);
        var endpoint = context.Request.Path.Value ?? "/";

        try
        {
            var result = await rateLimiterService.CheckRateLimit(clientId, endpoint);

            if (_options.EnableHeaders)
            {
                context.Response.Headers["X-Ratelimit-Limit"] = result.Limit.ToString();
                context.Response.Headers["X-Ratelimit-Remaining"] = result.Remaining.ToString();
                if (!result.IsAllowed)
                    context.Response.Headers["X-Ratelimit-Retry-After"] = result.RetryAfterSeconds.ToString();
            }

            if (!result.IsAllowed)
            {
                _logger.LogWarning("Rate limit exceeded for client {ClientId} on {Endpoint}", clientId, endpoint);
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    $"{{\"error\":\"Too Many Requests\",\"retryAfter\":{result.RetryAfterSeconds}}}");
                return;
            }
        }
        catch (Exception ex)
        {
            // Fail-open: if Redis is unavailable, allow the request through
            _logger.LogError(ex, "Rate limiter error for client {ClientId} — failing open", clientId);
        }

        await _next(context);
    }
}
