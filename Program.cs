using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OnlineExam.Domain;
using OnlineExam.Domain.Entities;
using OnlineExam.Domain.Interfaces;
using OnlineExam.Features.Accounts.Commands;
using OnlineExam.Features.Accounts.Endpoints;
using OnlineExam.Features.Categories.Endpoints;
using OnlineExam.Features.Dashboard.Endpoints;
using OnlineExam.Features.Exams.Endpoints;
using OnlineExam.Features.Profile.Endpoints;
using OnlineExam.Features.Questions.Endpoints;
using OnlineExam.Features.UserAnswers.Endpoints;
using OnlineExam.Infrastructure.ApplicationDBContext;
using OnlineExam.Infrastructure.Repositories;
using OnlineExam.Infrastructure.UnitOfWork;
using OnlineExam.Middlewares;
using OnlineExam.Shared.Data;
using OnlineExam.Shared.Helpers;
using Serilog;
using Serilog.Events;
using System.Configuration;
using System.Reflection;
using System.Text;

namespace OnlineExam
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Serilog setup (unchanged)
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "OnlineExam.Api")
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} | {CorrelationId} | {Message:lj}{NewLine}{Exception}", theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Literate)
                .WriteTo.File("logs/OnlineExam-.log", rollingInterval: RollingInterval.Day, outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {SourceContext} | {CorrelationId} | {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Information("Starting OnlineExam API application");

            var builder = WebApplication.CreateBuilder(args);

            builder.Host.UseSerilog();
            builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);
            // Services (unchanged)


            builder.Services.Configure<JWT>(builder.Configuration.GetSection("JWT"));
            var JwtOption = builder.Configuration.GetSection("JWT").Get<JWT>();
            builder.Services.AddSingleton(JwtOption);

            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => { /* unchanged */ })
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();



            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme; // Add this
            })
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters()
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidIssuer = JwtOption.Issuer,
                ValidAudience = JwtOption.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(JwtOption.Secretkey)),
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    Console.WriteLine($"🔴 AUTH FAILED: {context.Exception.Message}");
                    Console.WriteLine($"🔴 Exception: {context.Exception}");
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    Console.WriteLine($"🟢 TOKEN VALIDATED");
                    Console.WriteLine($"🟢 User: {context.Principal.Identity.Name}");
                    Console.WriteLine($"🟢 IsAuthenticated: {context.Principal.Identity.IsAuthenticated}");
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    Console.WriteLine($"🟡 CHALLENGE: {context.Error}");
                    Console.WriteLine($"🟡 Description: {context.ErrorDescription}");
                    return Task.CompletedTask;
                },
                OnMessageReceived = context =>
                {
                    Console.WriteLine($"🔵 MESSAGE RECEIVED: {context.Token}");
                    return Task.CompletedTask;
                }
            };
        });


            builder.Services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
                options.EnableSensitiveDataLogging(false);
                options.EnableServiceProviderCaching();
                options.EnableDetailedErrors(builder.Environment.IsDevelopment());
                options.LogTo(message => Log.Debug("[EF] {Message}", message), LogLevel.Warning);
            });

            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = builder.Configuration.GetValue<string>("RedisCache:Configuration");
                options.InstanceName = builder.Configuration.GetValue<string>("RedisCache:InstanceName");
            });

            builder.Services.AddMemoryCache();
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddMediatR(typeof(Program).Assembly);

            // UnitOfWork first
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Add this with your other service registrations
            builder.Services.AddScoped<TransactionMiddleware>();

            // Dynamic generic repositories for BaseEntity subclasses
            var baseEntityAssembly = Assembly.GetAssembly(typeof(BaseEntity)) ?? Assembly.GetExecutingAssembly();  // Fallback if null
            var entityTypes = baseEntityAssembly
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(BaseEntity)))
                .ToList();

            foreach (var entityType in entityTypes)
            {
                var interfaceType = typeof(IGenericRepository<>).MakeGenericType(entityType);
                var implementationType = typeof(GenericRepository<>).MakeGenericType(entityType);
                builder.Services.AddScoped(interfaceType, implementationType);
            }

            // Log for debugging (optional)
            Log.Information("Registered {Count} generic repositories for entities: {Entities}", entityTypes.Count, string.Join(", ", entityTypes.Select(t => t.Name)));
            builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    Log.Information("📊 Starting database seeding...");


                    await DatabaseSeeder.SeedAsync(services);
                    Log.Information("🌱 Database seeding completed successfully");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "❌ An error occurred while seeding the database");

                    if (app.Environment.IsDevelopment())
                    {
                        throw;
                    }

                    Log.Warning("⚠️ Application will continue without seeding");
                }
            }
            app.UseMiddleware<ErrorHandlingMiddleware>();


            // Trust the dev certificate on startup
            if (app.Environment.IsDevelopment())
            {
                var cert = new HttpClient().GetAsync("https://localhost:5001").ContinueWith(task => { });
                app.UseDeveloperExceptionPage(); // Optional: for detailed errors
            }
            app.UseStaticFiles();
            app.UseHttpsRedirection();
            app.UseRouting();
            // Add this BEFORE authentication
            app.Use(async (context, next) =>
            {
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                Console.WriteLine($"[DEBUG] Auth Header: {authHeader}");
                Log.Information("Auth Header: {AuthHeader}", authHeader);
                await next();
            });

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseMiddleware<FormContentTypeValidationMiddleware>();
            app.UseMiddleware<TransactionMiddleware>();
            app.UseMiddleware<RateLimitingMiddleware>();
            app.UseMiddleware<ProfilingMiddleware>();

            app.MapControllers();
            app.MapGet("/", () => "OnlineExam API is running...");
            app.MapRegisterEndpoint(); // Map the register endpoint
            app.MapLoginEndpoint(); // Map the login endpoint
            app.MapConfirmEmailEndpoint(); // Map the confirm email endpoint
            app.MapLogoutEndpoint(); // Map the logout endpoint
            app.MapForgotPasswordEndpoint(); // Map the forgot password endpoint
            app.MapResetPasswordEndpoint(); // Map the reset password endpoint
            app.MapResendVerificationCodeEndpoint(); // Map the resend verification code endpoint
           //profile endpoints
            app.MapProfileEndpoint();
            app.MapUpdateProfileEndpoint();
            //category endpoints
            app.MapGetUserCategoriesEndpoint();
            app.MapGetCategoriesForAdminEndpoint();
            app.MapGetCategoryByIdEndpoint();
            app.MapCreateCategoryEndpoint();
            app.MapUpdateCategoryEndpoint();
            app.MapDeleteCategoryEndpoint();
            // Register Exam endpoints
            app.MapUserExamEndpoints();
            app.MapAdminExamEndpoints();
            app.MapGetExamByIDEndpoint();
            app.MapStartExamAttemptEndpoint();
            app.MapSubmitExamEndpoint();
            app.MapCreateExamEndpoint();
            app.MapDeleteExamEndpoint();
            app.MapEditExamEndpoint();
            // Register Question endpoints
            app.MapAdminQuestionEndpoint();
            app.MapAddQuestionEndpoint();
            app.MapUpdateQuestionEndpoint();
            app.MapDeleteQuestionEndpoint();
            app.MapGetQuestionDetailsEndpoint();
            // Register UserAnswer endpoints
            app.MapGetAllUserAnswersEndpoints();
            app.MapGetDetailedUserAnswerEndpoint();
            // Register Dashboard endpoints
            app.MapDashboardStatsEndpoints();
            app.MapMostActiveExamsEndpoints();
            app.MapMostActiveCategoriesEndpoints();

            app.Use(async (ctx, next) =>
            {
                try { await next(); }
                catch (FluentValidation.ValidationException ex)
                {
                    var dict = ex.Errors
                        .GroupBy(e => e.PropertyName ?? "")
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                    await Results.ValidationProblem(dict).ExecuteAsync(ctx);
                }
            });


            app.Run();

        }
    }
}