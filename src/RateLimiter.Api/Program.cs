using RateLimiter.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddRateLimiter(builder.Configuration);

var app = builder.Build();

app.UseCustomRateLimiter();
app.MapControllers();

app.Run();

public partial class Program { }
