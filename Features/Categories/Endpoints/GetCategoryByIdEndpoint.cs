using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using OnlineExam.Features.Categories.Dtos;
using OnlineExam.Features.Categories.Queries;
using OnlineExam.Shared.Responses;
using System.Text.Json;

namespace OnlineExam.Features.Categories.Endpoints
{
    public static class GetCategoryByIdEndpoint
    {
        public static void MapGetCategoryByIdEndpoint(this WebApplication app)
        {
            app.MapGet("/api/categories/{id}", async (int id, IMediator mediator, IDistributedCache cache) =>
            {
                var cacheKey = $"categories:details:{id}";
                var cachedData = await cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cachedData))
                {
                    var cachedResult = JsonSerializer.Deserialize<ServiceResponse<CategoryDetailsDto>>(cachedData);
                    if (cachedResult is not null)
                    {
                        return Results.Json(cachedResult, statusCode: cachedResult.StatusCode);
                    }
                }

                var result = await mediator.Send(new GetCategoryByIdQuery(id));
                if (result.StatusCode == StatusCodes.Status200OK)
                {
                    var cacheOptions = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                    };
                    await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), cacheOptions);
                }

                return Results.Json(result, statusCode: result.StatusCode);
            })
            .AllowAnonymous()
            .WithName("GetCategoryById")
            .WithTags("Categories")
            .Produces<ServiceResponse<object>>(StatusCodes.Status200OK)
            .Produces<ServiceResponse<object>>(StatusCodes.Status404NotFound);
        }
    }
}
