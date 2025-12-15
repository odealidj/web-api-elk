using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using web_api_elk.Data;
using web_api_elk.Middleware;
using web_api_elk.Models;
using web_api_elk.Repositories;
using web_api_elk.Services;

// Bootstrap Serilog from configuration
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services);
});

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// Rate limiting dependencies
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IRateLimiter>(sp => new InMemoryRateLimiter(sp.GetRequiredService<IMemoryCache>(), maxAttempts: 5, window: TimeSpan.FromMinutes(5)));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();

app.MapPost("/api/auth/register", async (RegisterRequest request, HttpContext httpContext, IAuthService authService, CancellationToken cancellationToken) =>
    {
        var correlationId = httpContext.Items["X-Correlation-Id"] as string;
        var result = await authService.RegisterAsync(request, correlationId, cancellationToken);

        var response = new SimpleResponse
        {
            Success = result.Success,
            Message = result.Message,
            CorrelationId = correlationId
        };

        return result.Success
            ? Results.Created("/api/auth/register", response)
            : Results.BadRequest(response);
    })
    .WithName("Register")
    .WithOpenApi();

app.MapPost("/api/auth/login", async (LoginRequest request, HttpContext httpContext, IAuthService authService, IRateLimiter rateLimiter, CancellationToken cancellationToken) =>
    {
        var correlationId = httpContext.Items["X-Correlation-Id"] as string;

        // Build rate limit key: IP + username
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var key = $"login:{ip}:{request.Username}";

        if (!rateLimiter.IsAllowed(key, out var retryAfter))
        {
            var limitedResponse = new SimpleResponse
            {
                Success = false,
                Message = "Too many login attempts. Please try again later.",
                CorrelationId = correlationId
            };

            if (retryAfter > TimeSpan.Zero)
            {
                httpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
            }

            return Results.Json(limitedResponse, statusCode: StatusCodes.Status429TooManyRequests);
        }

        var result = await authService.LoginAsync(request, correlationId, cancellationToken);

        var response = new SimpleResponse
        {
            Success = result.Success,
            Message = result.Message,
            CorrelationId = correlationId
        };

        if (!result.Success)
        {
            return Results.Json(response, statusCode: StatusCodes.Status401Unauthorized);
        }

        return Results.Ok(response);
    })
    .WithName("Login")
    .WithOpenApi();

app.Run();
