using HarborTorrent.Api.Contracts;
using HarborTorrent.Domain.Common;

namespace HarborTorrent.Api.Endpoints;

internal static class EndpointExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, HttpContext httpContext, int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            return Results.Json(ApiResponseFactory.Success(httpContext, result.Value), statusCode: successStatusCode);
        }

        return Results.Json(ApiResponseFactory.Failure<object?>(httpContext, result.Errors), statusCode: GetStatusCode(result.Errors));
    }

    public static IResult ToHttpResult(this Result result, HttpContext httpContext, object? payload = null, int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            return Results.Json(ApiResponseFactory.Success(httpContext, payload), statusCode: successStatusCode);
        }

        return Results.Json(ApiResponseFactory.Failure<object?>(httpContext, result.Errors), statusCode: GetStatusCode(result.Errors));
    }

    private static int GetStatusCode(IReadOnlyCollection<Error> errors)
    {
        var firstError = errors.FirstOrDefault();
        return firstError?.Code switch
        {
            "validation" => StatusCodes.Status400BadRequest,
            "not_found" => StatusCodes.Status404NotFound,
            "conflict" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
    }
}

internal static class ApiResponseFactory
{
    public static ApiResponse<T> Success<T>(HttpContext httpContext, T? data) => new()
    {
        Success = true,
        Data = data,
        Errors = Array.Empty<ApiError>(),
        RequestId = httpContext.TraceIdentifier
    };

    public static ApiResponse<T> Failure<T>(HttpContext httpContext, IEnumerable<Error> errors) => new()
    {
        Success = false,
        Data = default,
        Errors = errors.Select(error => new ApiError(error.Code, error.Message)).ToArray(),
        RequestId = httpContext.TraceIdentifier
    };

    public static ApiResponse<T> Failure<T>(HttpContext httpContext, IEnumerable<ApiError> errors) => new()
    {
        Success = false,
        Data = default,
        Errors = errors.ToArray(),
        RequestId = httpContext.TraceIdentifier
    };
}
