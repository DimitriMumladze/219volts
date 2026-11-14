namespace TwoNineteenVolts.Venue.Application.Auth;

// Login failures shown to the user, as keys into Resources/Messages.resx.
public static class AuthError
{
    public const string WrongPin = "AuthWrongPin";
    public const string TryAgainInOneSecond = "AuthTryAgainInOneSecond";

    // The text has {0} for the number of seconds.
    public const string TryAgainInSeconds = "AuthTryAgainInSeconds";
}
