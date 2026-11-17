namespace TwoNineteenVolts.Api.Common;

public record Result<T>(T? Value, string? Error)
{
    public bool IsFailure => Error is not null;

    public static Result<T> Ok(T value) => new(value, null);
    public static Result<T> Fail(string error) => new(default, error);
}
