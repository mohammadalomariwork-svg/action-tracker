using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Middleware;

/// <summary>
/// Global exception handler that converts unhandled exceptions to RFC 7807
/// ProblemDetails responses. Stack traces are suppressed in Production.
/// </summary>
public class ExceptionMiddleware : IMiddleware
{
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionMiddleware(ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
    {
        _logger = logger;
        _env    = env;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var (statusCode, title) = ex switch
        {
            // WO-2026-11 P-2: visible project, link does not allow the change (before its base type).
            ActionTracker.Application.Features.ProjectPeople.ProjectAccessDeniedException => (StatusCodes.Status403Forbidden, "Forbidden"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized,          "Unauthorized"),
            ArgumentException           => (StatusCodes.Status400BadRequest,            "Bad Request"),
            KeyNotFoundException        => (StatusCodes.Status404NotFound,              "Not Found"),
            InvalidOperationException   => (StatusCodes.Status422UnprocessableEntity,   "Unprocessable Entity"),
            _                           => (StatusCodes.Status500InternalServerError,   "Internal Server Error"),
        };

        // Log server errors at Error level, client errors at Warning
        if (statusCode >= 500)
            _logger.LogError(ex, "Unhandled server exception: {Message}", ex.Message);
        else
            _logger.LogWarning(ex, "Handled client exception ({Status}): {Message}", statusCode, ex.Message);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title  = title,
            // In production only the title is surfaced; detail is available in non-production
            Detail = _env.IsProduction() ? title : ex.Message,
        };

        // WO-2026-11 P-2: the project-access reason is written for users; the client shows `message`.
        if (ex is ActionTracker.Application.Features.ProjectPeople.ProjectAccessDeniedException)
            problemDetails.Extensions["message"] = ex.Message;

        // Stack trace only in non-production environments
        if (!_env.IsProduction() && ex.StackTrace is not null)
            problemDetails.Extensions["stackTrace"] = ex.StackTrace;

        context.Response.StatusCode  = statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}
