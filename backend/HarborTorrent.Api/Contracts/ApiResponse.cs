namespace HarborTorrent.Api.Contracts;

internal sealed class ApiResponse<T>
{
    public required bool Success { get; init; }

    public T? Data { get; init; }

    public required IReadOnlyCollection<ApiError> Errors { get; init; }

    public required string RequestId { get; init; }
}