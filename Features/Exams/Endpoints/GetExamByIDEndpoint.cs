using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using OnlineExam.Features.Exams.Dtos;
using OnlineExam.Features.Exams.Queries;
using OnlineExam.Shared.Responses;
using System.Text.Json;

namespace OnlineExam.Features.Exams.Endpoints
{
    public static class GetExamByIDEndpoint
    {
        public static void MapGetExamByIDEndpoint(this WebApplication app)
        {
            var group = app.MapGroup("/api/exams")
                .WithTags("Exams");

            // GET /api/exams/{id} - Get exam details
            group.MapGet("/{id}", async (int id, IMediator mediator, IDistributedCache cache) =>
            {
                string cacheKey = $"Exam_{id}";
                var cachedData = await cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    var cachedResult = JsonSerializer.Deserialize<ServiceResponse<UserExamDetailsDto>>(cachedData);
                    if (cachedResult is not null)
                    {
                        return Results.Json(cachedResult, statusCode: cachedResult.StatusCode);
                    }
                }

                var result = await mediator.Send(new GetExamDetailsQuery(id));

                // Only cache successful requests
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
            .WithName("GetExamDetails")
            .Produces<ServiceResponse<UserExamDetailsDto>>(StatusCodes.Status200OK)
            .Produces<ServiceResponse<UserExamDetailsDto>>(StatusCodes.Status404NotFound);
        }

    }
}
