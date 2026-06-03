using MediatR;
using OnlineExam.Features.Accounts.Dtos;
using System.ComponentModel.DataAnnotations;

namespace OnlineExam.Features.Accounts.Endpoints
{
    public static class LoginEndpoint
    {
        public static void MapLoginEndpoint(this WebApplication app)
        {
            app.MapPost("/api/accounts/login", async (LoginReqDTO request, IMediator mediator) =>
            {
                var validationContext = new ValidationContext(request);
                var validationResults = new List<ValidationResult>();
                if (!Validator.TryValidateObject(request, validationContext, validationResults, true))
                {
                    var errors = validationResults
                        .SelectMany(v => v.MemberNames.DefaultIfEmpty(string.Empty)
                            .Select(member => new { member, v.ErrorMessage }))
                        .GroupBy(x => x.member)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(x => x.ErrorMessage ?? "Invalid value").Distinct().ToArray());

                    return Results.ValidationProblem(errors);
                }

                var result = await mediator.Send(new Commands.LoginCommand(request));

                return Results.Json(result, statusCode: result.StatusCode);
            })
            .WithName("Login")
            .WithTags("Accounts");
        }

    }
}
