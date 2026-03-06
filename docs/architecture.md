# Rate Limiter - Architecture

## Overview

This service implements a rate limiter using .NET 8 and Clean Architecture principles.

## Dependency Direction

```
Api → Application → Domain
Infrastructure → Application → Domain
```

## Layers

| Layer | Project | Responsibility |
|-------|---------|----------------|
| Domain | `RateLimiter.Domain` | Entities, value objects, domain rules — no external dependencies |
| Application | `RateLimiter.Application` | Use cases, interfaces, DTOs, CQRS handlers |
| Infrastructure | `RateLimiter.Infrastructure` | DB context, repositories, external services |
| API | `RateLimiter.Api` | HTTP controllers, middleware, DI wiring |

## Rate Limiting Strategies

_To be documented as strategies are implemented._
