using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Application.Auth.Contracts;

public record LoginResult(User? User, TimeSpan? RetryAfter)
{
    public static LoginResult WrongCredentials { get; } = new(null, null);

    public static LoginResult Success(User user) => new(user, null);

    public static LoginResult Throttled(TimeSpan wait) => new(null, wait);
}
