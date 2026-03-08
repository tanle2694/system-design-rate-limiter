using RateLimiter.Application.Interfaces;

namespace RateLimiter.Api.Services;

public sealed class HttpContextClientIdentifier : IClientIdentifier
{
    public string GetClientId(HttpContext context)
    {
        // 1. Check X-Forwarded-For (client behind proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
            return forwardedFor.Split(',')[0].Trim();

        // 2. Check Authorization header (extract user ID from Bearer token sub claim)
        var authorization = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(authorization) && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var userId = context.User.FindFirst("sub")?.Value
                      ?? context.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            if (!string.IsNullOrWhiteSpace(userId))
                return $"user:{userId}";
        }

        // 3. Check X-Api-Key header
        var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(apiKey))
            return $"apikey:{apiKey}";

        // 4. Fall back to remote IP address
        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip:{remoteIp}";
    }
}
