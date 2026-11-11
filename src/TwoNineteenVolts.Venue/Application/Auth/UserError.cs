namespace TwoNineteenVolts.Venue.Application.Auth;

// Expected failures of user management. The text doubles as the ProblemDetails title.
public static class UserError
{
    public const string NotFound = "User not found";
    public const string DuplicateName = "Another user already has this name";
    public const string LastManager = "The last active manager cannot be removed or demoted";
    public const string InvalidPin = "PIN must be exactly 4 digits";
    public const string InvalidName = "Name must not be empty";
    public const string InvalidRole = "Role must be Staff or Manager";
}
