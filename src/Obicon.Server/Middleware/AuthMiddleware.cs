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
            await context.Response.WriteAsync("Unauthorized - Missing or invalid Authorization header");
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
}
