using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using OnlineExam.Features.Exams.Dtos;
using OnlineExam.Features.Exams.Queries;
using OnlineExam.Shared.Responses;
using System.Text.Json;

namespace OnlineExam.Features.Exams.Endpoints
{
    public static class GetUserExamsEndpoint
    {
        public static void MapUserExamEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/exams")
                .WithTags("Exams");
                

            // GET /api/exams - List exams (with optional category filter)
            group.MapGet("/", async (
                IMediator mediator,
                IDistributedCache cache,
                [AsParameters] GetExamsQuery query) =>
            {
                var cacheKey = $"exams:list:{query.PageNumber}:{query.PageSize}:{query.CategoryId?.ToString() ?? "all"}";
                var cachedData = await cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cachedData))
                {
                    var cachedResult = JsonSerializer.Deserialize<ServiceResponse<PagedResult<UserExamDto>>>(cachedData);
                    if (cachedResult is not null)
                    {
                        return Results.Json(cachedResult, statusCode: cachedResult.StatusCode);
                    }
                }

                var result = await mediator.Send(query);

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
            .WithName("GetExams")
            .Produces<ServiceResponse<PagedResult<UserExamDto>>>(StatusCodes.Status200OK);

            
        }
    }
}
