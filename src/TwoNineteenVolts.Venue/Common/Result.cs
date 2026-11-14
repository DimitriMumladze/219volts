using System.Diagnostics.CodeAnalysis;

namespace TwoNineteenVolts.Venue.Common;

public record Result<T>(T? Value, string? Error)
{
    [MemberNotNullWhen(true, nameof(Error))]
    [MemberNotNullWhen(false, nameof(Value))]
    public bool IsFailure => Error is not null;

    public static Result<T> Ok(T value) => new(value, null);
    public static Result<T> Fail(string error) => new(default, error);
}
