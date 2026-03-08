using Microsoft.AspNetCore.Http;

namespace RateLimiter.Application.Interfaces;

public interface IClientIdentifier
{
    string GetClientId(HttpContext context);
}
