FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY RateLimiter.sln .
COPY src/RateLimiter.Domain/RateLimiter.Domain.csproj src/RateLimiter.Domain/
COPY src/RateLimiter.Application/RateLimiter.Application.csproj src/RateLimiter.Application/
COPY src/RateLimiter.Infrastructure/RateLimiter.Infrastructure.csproj src/RateLimiter.Infrastructure/
COPY src/RateLimiter.Api/RateLimiter.Api.csproj src/RateLimiter.Api/

RUN dotnet restore src/RateLimiter.Api/RateLimiter.Api.csproj

COPY src/ src/
RUN dotnet publish src/RateLimiter.Api/RateLimiter.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "RateLimiter.Api.dll"]
