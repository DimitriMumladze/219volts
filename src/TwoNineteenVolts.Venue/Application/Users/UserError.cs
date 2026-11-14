namespace TwoNineteenVolts.Venue.Application.Users;

// Expected failures of user management, as keys into Resources/Messages.resx.
public static class UserError
{
    public const string NotFound = "UserNotFound";
    public const string DuplicateName = "UserDuplicateName";
    public const string ManagerCannotBeDeactivated = "UserManagerCannotBeDeactivated";
    public const string InvalidPin = "UserInvalidPin";
    public const string InvalidName = "UserInvalidName";
}
