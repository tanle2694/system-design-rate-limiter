# Distributed Rate Limiter

A distributed rate limiter service built with .NET 8, Redis Cluster, and Clean Architecture. Implements four rate-limiting algorithms with atomic Redis operations via Lua scripts.

## Rate Limiting Algorithms

| Algorithm | Description |
|-----------|-------------|
| **Token Bucket** | Tokens replenish at a fixed rate; each request consumes a token. Allows short bursts. |
| **Fixed Window Counter** | Counts requests in fixed time windows (e.g., per minute). Simple but can allow 2x burst at window boundaries. |
| **Sliding Window Log** | Tracks individual request timestamps in a sorted set. Most accurate but higher memory usage. |
| **Sliding Window Counter** | Combines current and previous window counts with overlap weighting. Good accuracy with low memory. |

## Architecture

Clean Architecture with four layers. Dependencies flow inward:

```
API → Application → Domain
Infrastructure → Application → Domain
```

| Layer | Project | Responsibility |
|-------|---------|----------------|
| Domain | `RateLimiter.Domain` | Entities, value objects, enums, algorithm interface. No external dependencies. |
| Application | `RateLimiter.Application` | Algorithm implementations, service orchestration, store/provider interfaces. |
| Infrastructure | `RateLimiter.Infrastructure` | Redis store with Lua scripts, YAML rule provider with caching. |
| API | `RateLimiter.Api` | ASP.NET Core middleware, DI registration, client identification. |

## Getting Started

### Prerequisites

- .NET 8 SDK
- Docker & Docker Compose (for running with Redis Cluster)

### Run with Docker

```bash
docker-compose -f docker/docker-compose.yaml up
```

This starts a 6-node Redis Cluster (3 masters + 3 replicas), two API instances, and an Nginx load balancer on port 8080.

### Run Locally

Requires a Redis Cluster running on `localhost:6379,6380,6381`.

```bash
dotnet run --project src/RateLimiter.Api/RateLimiter.Api.csproj
```

### Build

```bash
dotnet restore && dotnet build --configuration Release
```

## Configuration

### Application Settings

Configure in `src/RateLimiter.Api/appsettings.json`:

```json
{
  "RateLimiter": {
    "Enabled": true,
    "DefaultAlgorithm": "TokenBucket",
    "DefaultLimit": 100,
    "DefaultWindowSeconds": 60,
    "EnableHeaders": true,
    "RulesFile": "rules/rate-limit-rules.yaml"
  },
  "Redis": {
    "Endpoints": ["localhost:6379", "localhost:6380", "localhost:6381"],
    "InstanceName": "rate-limiter",
    "KeyPrefix": "rl:",
    "ConnectTimeout": 5000,
    "SyncTimeout": 5000
  }
}
```

### Rate Limit Rules

Rules are defined in YAML (`src/RateLimiter.Api/rules/rate-limit-rules.yaml`):

```yaml
domain: api
descriptors:
  - key: endpoint
    value: "/hello"
    rate_limit:
      algorithm: token_bucket
      unit: minute
      requests_per_unit: 10

  - key: endpoint
    value: "/api/default"
    rate_limit:
      algorithm: sliding_window_counter
      unit: second
      requests_per_unit: 5
```

## Key Design Decisions

- **Fail-open pattern**: If Redis is unavailable, requests are allowed through rather than blocked.
- **Redis Cluster**: 6-node cluster (3 masters + 3 replicas) for high availability. Keys use hash tags `{clientKey:domain:descriptor:value}` to ensure colocation on the same hash slot for multi-key Lua scripts.
- **Lua scripts**: All Redis operations are atomic via Lua scripts to prevent race conditions.
- **Client identification chain**: X-Forwarded-For → Authorization header (Bearer token) → X-Api-Key → Remote IP address.
- **Response headers**: `X-Ratelimit-Limit`, `X-Ratelimit-Remaining`, and `X-Ratelimit-Retry-After` (on 429 responses).
- **Rule caching**: YAML rules are cached for 1 minute with thread-safe refresh.

## Testing

```bash
# Run unit tests
dotnet test tests/RateLimiter.UnitTests/RateLimiter.UnitTests.csproj

# Run integration tests (requires Redis Cluster)
dotnet test tests/RateLimiter.IntegrationTests/RateLimiter.IntegrationTests.csproj

# Run API test script (requires running API)
./scripts/test-api.sh
```

## Project Structure

```
src/
  RateLimiter.Domain/           # Entities, value objects, enums, interfaces
  RateLimiter.Application/      # Algorithm implementations, services, interfaces
  RateLimiter.Infrastructure/   # Redis store, Lua scripts, YAML rule provider
  RateLimiter.Api/              # Middleware, controllers, DI extensions
tests/
  RateLimiter.UnitTests/        # Domain layer unit tests
  RateLimiter.IntegrationTests/ # Integration tests (requires Redis)
docker/
  docker-compose.yaml           # Redis Cluster + API + Nginx
  Dockerfile                    # Multi-stage .NET build
  nginx.conf                    # Load balancer config
scripts/
  test-api.sh                   # API test scenarios
```

## Tech Stack

- .NET 8 / ASP.NET Core
- Redis Cluster (StackExchange.Redis)
- Nginx (load balancer)
- Docker & Docker Compose
- xUnit (testing)
- YamlDotNet (rule configuration)
