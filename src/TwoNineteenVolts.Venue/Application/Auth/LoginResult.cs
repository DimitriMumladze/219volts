using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Application.Auth;

// Exactly one outcome: User set = success; RetryAfter set = throttled; neither = wrong credentials.
public record LoginResult(User? User, TimeSpan? RetryAfter)
{
    public static LoginResult WrongCredentials { get; } = new(null, null);

    public static LoginResult Success(User user) => new(user, null);

    public static LoginResult Throttled(TimeSpan wait) => new(null, wait);
}
