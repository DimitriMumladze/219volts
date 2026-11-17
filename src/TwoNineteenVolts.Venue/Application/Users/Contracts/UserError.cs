namespace TwoNineteenVolts.Venue.Application.Users.Contracts;

public static class UserError
{
    public const string NotFound = "UserNotFound";
    public const string DuplicateName = "UserDuplicateName";
    public const string ManagerCannotBeDeactivated = "UserManagerCannotBeDeactivated";
    public const string InvalidPin = "UserInvalidPin";
    public const string InvalidName = "UserInvalidName";
}
