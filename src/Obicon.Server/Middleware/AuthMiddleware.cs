using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Obicon.Server.Services;

namespace Obicon.Server.Middleware;

public class AuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServerSettingsService _settingsService;
    private readonly ILogger<AuthMiddleware> _logger;

    public AuthMiddleware(RequestDelegate next, IServerSettingsService settingsService, ILogger<AuthMiddleware> logger)
    {
        _next = next;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsExcludedPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var expected = await _settingsService.GetAsync<string>("AuthHeader");

        if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader) ||
            authHeader != expected)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Missing or invalid Authorization header"
            }, JsonOptions));
            return;
        }

        await _next(context);
    }

    private static bool IsExcludedPath(PathString path)
    {
        // /ws/nodes authenticates with the node token and /v1/enroll with the enroll token
        // instead of the API Authorization header
        var excludedPaths = new[] { "/metrics", "/swagger", "/swagger-ui", "/ws", "/v1/enroll" };
        return excludedPaths.Any(p => path.StartsWithSegments(p));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
