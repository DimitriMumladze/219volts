using System.Diagnostics.CodeAnalysis;

namespace TwoNineteenVolts.Venue.Common;

public record Result(string? Error)
{
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => Error is not null;

    public static Result Ok() => new((string?)null);
    public static Result Fail(string error) => new(error);
}
