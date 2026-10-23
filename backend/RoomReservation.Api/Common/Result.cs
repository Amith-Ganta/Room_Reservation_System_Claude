namespace RoomReservation.Api.Common;

public enum ResultStatus
{
    Ok,
    Invalid,
    NotFound,
    Conflict
}

/// <summary>
/// Outcome of a service call. Services return this instead of throwing, so the controller
/// is the only place that decides which HTTP status a failure maps to.
/// </summary>
public sealed class Result<T>
{
    private Result(ResultStatus status, T? value, IReadOnlyList<ApiError> errors)
    {
        Status = status;
        Value = value;
        Errors = errors;
    }

    public ResultStatus Status { get; }
    public T? Value { get; }
    public IReadOnlyList<ApiError> Errors { get; }
    public bool IsSuccess => Status == ResultStatus.Ok;

    public static Result<T> Ok(T value) => new(ResultStatus.Ok, value, Array.Empty<ApiError>());

    public static Result<T> Invalid(IReadOnlyList<ApiError> errors)
    {
        // An Invalid result without errors is a programming mistake, not an expected failure.
        if (errors.Count == 0)
        {
            throw new ArgumentException("An invalid result needs at least one error.", nameof(errors));
        }

        return new(ResultStatus.Invalid, default, errors);
    }

    public static Result<T> NotFound(ApiError error) => new(ResultStatus.NotFound, default, [error]);

    public static Result<T> Conflict(ApiError error) => new(ResultStatus.Conflict, default, [error]);
}
