namespace HarborTorrent.Domain.Common;

/// <summary>
/// Represents a business error that can be returned to callers without throwing an exception.
/// </summary>
public sealed record Error(string Code, string Message)
{
    public static Error Validation(string message) => new("validation", message);

    public static Error NotFound(string message) => new("not_found", message);

    public static Error Conflict(string message) => new("conflict", message);
}

/// <summary>
/// Represents the outcome of a domain or application operation.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, IReadOnlyCollection<Error> errors)
    {
        IsSuccess = isSuccess;
        Errors = errors;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public IReadOnlyCollection<Error> Errors { get; }

    public static Result Success() => new(true, Array.Empty<Error>());

    public static Result Failure(params Error[] errors) => new(false, errors);

    public static Result Failure(IReadOnlyCollection<Error> errors) => new(false, errors);
}

/// <summary>
/// Represents the outcome of a domain or application operation with a return value.
/// </summary>
public sealed class Result<T> : Result
{
    private Result(T value)
        : base(true, Array.Empty<Error>())
    {
        Value = value;
    }

    private Result(IReadOnlyCollection<Error> errors)
        : base(false, errors)
    {
    }

    public T? Value { get; }

    public static Result<T> Success(T value) => new(value);

    public new static Result<T> Failure(params Error[] errors) => new(errors);

    public new static Result<T> Failure(IReadOnlyCollection<Error> errors) => new(errors);
}