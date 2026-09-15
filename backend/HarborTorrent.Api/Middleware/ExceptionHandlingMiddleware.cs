using FluentValidation;
using HarborTorrent.Api.Contracts;

namespace HarborTorrent.Api.Middleware;

internal sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException validationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            var response = new ApiResponse<object?>
            {
                Success = false,
                Data = null,
                Errors = validationException.Errors
                    .Select(error => new ApiError("validation", error.ErrorMessage))
                    .ToArray(),
                RequestId = context.TraceIdentifier
            };

            await context.Response.WriteAsJsonAsync(response);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception while processing request {RequestId}", context.TraceIdentifier);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var response = new ApiResponse<object?>
            {
                Success = false,
                Data = null,
                Errors = new[] { new ApiError("server_error", "An unexpected error occurred.") },
                RequestId = context.TraceIdentifier
            };

            await context.Response.WriteAsJsonAsync(response);
        }
    }
}