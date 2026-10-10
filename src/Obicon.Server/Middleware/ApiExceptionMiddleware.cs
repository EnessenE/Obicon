using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Obicon.Server.Services;

namespace Obicon.Server.Middleware;

/// <summary>
/// Maps unhandled service exceptions to RFC 9457 ProblemDetails, so every error of the
/// API shares one shape: ArgumentException becomes 400, UnauthorizedAccessException 401,
/// ForbiddenException 403, InvalidOperationException 409, and anything else 500 with
/// the exception logged. Controllers stay free of try/catch blocks.
/// </summary>
public partial class ApiExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger, IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException or ForbiddenException
                                       or InvalidOperationException or KeyNotFoundException)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            var (status, title) = ex switch
            {
                ArgumentException => (StatusCodes.Status400BadRequest, "Bad request"),
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
                ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
                InvalidOperationException => (StatusCodes.Status409Conflict, "Conflict"),
                KeyNotFoundException => (StatusCodes.Status404NotFound, "Not found"),
                _ => (StatusCodes.Status500InternalServerError, "Internal server error")
            };

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = ex.Message,
                Instance = context.Request.Path
            }, JsonOptions));
        }
        catch (Exception ex)
        {
            Log.UnhandledException(_logger, ex, context.Request.Method, context.Request.Path);
            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                // The details of an unexpected failure are never surfaced to the client
                Detail = _environment.IsDevelopment() ? ex.Message : "An unexpected error occurred.",
                Instance = context.Request.Path
            }, JsonOptions));
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
        public static partial void UnhandledException(ILogger logger, Exception ex, string method, string path);
    }
}
