using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using OnlineExam.Features.Categories.Dtos;
using OnlineExam.Features.Categories.Queries;
using OnlineExam.Shared.Responses;
using System.Text.Json;

namespace OnlineExam.Features.Categories.Endpoints
{
    public static class GetUserCategoriesEndpoint
    {
        public static void MapGetUserCategoriesEndpoint(this WebApplication app)
        {
            app.MapGet("/api/categories/user", async (
                IMediator mediator,
                IDistributedCache cache,
                [FromQuery] int pageNumber = 1,
                [FromQuery] int pageSize = 20
                ) =>
            {
                var cacheKey = $"categories:list:{pageNumber}:{pageSize}";
                var cachedData = await cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cachedData))
                {
                    var cachedResult = JsonSerializer.Deserialize<ServiceResponse<PagedResult<UserCategoryDto>>>(cachedData);
                    if (cachedResult is not null)
                    {
                        return Results.Json(cachedResult, statusCode: cachedResult.StatusCode);
                    }
                }

                var result = await mediator.Send(new GetUserCategoriesQuery(pageNumber, pageSize));
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
            .WithName("GetUserCategories")
            .WithTags("Categories")
            .Produces<ServiceResponse<object>>(StatusCodes.Status200OK)
            .Produces<ServiceResponse<object>>(StatusCodes.Status404NotFound);

        }
    }
}
