namespace Obicon.Server.Middleware;

public class AuthMiddleware
{
    private readonly RequestDelegate _next;

    public AuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader) ||
            authHeader != "uwu")
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized - Missing or invalid Authorization header");
            return;
        }

        await _next(context);
    }
}
