using Microsoft.AspNetCore.Mvc;

namespace RateLimiter.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class HelloController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { message = "Hello from Rate Limiter API!" });
}
