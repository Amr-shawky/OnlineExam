using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace OnlineExam.Features.Accounts.Endpoints
{
    public static class TestEndpoint
    {
        private sealed record RedisCacheTestPayload(string Value, DateTime CreatedAtUtc);

        public static void MapTestEndpoint(this WebApplication app)
        {
            app.MapGet("/api/cache/test", async (
                IDistributedCache cache,
                [FromQuery] string key = "default",
                [FromQuery] int ttlSeconds = 60) =>
            {
                var safeTtl = Math.Clamp(ttlSeconds, 10, 3600);
                var cacheKey = $"cache:test:{key}";
                var cachedData = await cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    var payload = JsonSerializer.Deserialize<RedisCacheTestPayload>(cachedData);
                    return Results.Ok(new
                    {
                        CacheKey = cacheKey,
                        Source = "redis",
                        TtlSeconds = safeTtl,
                        Data = payload
                    });
                }

                var newPayload = new RedisCacheTestPayload(
                    Value: $"generated-{Guid.NewGuid():N}",
                    CreatedAtUtc: DateTime.UtcNow);

                await cache.SetStringAsync(
                    cacheKey,
                    JsonSerializer.Serialize(newPayload),
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(safeTtl)
                    });

                return Results.Ok(new
                {
                    CacheKey = cacheKey,
                    Source = "generated",
                    TtlSeconds = safeTtl,
                    Data = newPayload
                });
            })
            .AllowAnonymous()
            .WithName("TestRedisCache")
            .WithTags("Cache Test");
        }
    }
}
