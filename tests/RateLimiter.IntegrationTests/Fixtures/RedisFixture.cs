using StackExchange.Redis;

namespace RateLimiter.IntegrationTests.Fixtures;

public sealed class RedisFixture : IAsyncLifetime
{
    public IConnectionMultiplexer Connection { get; private set; } = null!;

    public string Endpoint =>
        Environment.GetEnvironmentVariable("REDIS_ENDPOINT") ?? "localhost:6379";

    public async Task InitializeAsync()
    {
        var config = new ConfigurationOptions
        {
            EndPoints = { Endpoint },
            ConnectTimeout = 5000,
            SyncTimeout = 5000,
            AbortOnConnectFail = false,
        };

        Connection = await ConnectionMultiplexer.ConnectAsync(config);

        if (!Connection.IsConnected)
            throw new InvalidOperationException(
                $"Could not connect to Redis at {Endpoint}. Ensure Redis is running.");

        await Connection.GetDatabase().PingAsync();
    }

    public async Task DisposeAsync()
    {
        if (Connection is { IsConnected: true })
        {
            var db = Connection.GetDatabase();
            await db.ExecuteAsync("FLUSHDB");
        }

        Connection?.Dispose();
    }

    public async Task FlushDb()
    {
        var db = Connection.GetDatabase();
        await db.ExecuteAsync("FLUSHDB");
    }
}
