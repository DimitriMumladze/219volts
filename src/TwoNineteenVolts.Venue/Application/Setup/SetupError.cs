namespace TwoNineteenVolts.Venue.Application.Setup;

// Expected failures of installing a venue and resetting the Support password.
public static class SetupError
{
    public const string AlreadyDone = "Setup has already been done: users exist";
    public const string InvalidName = "The name must not be empty";
    public const string ReservedName = $"\"{SetupService.SupportDisplayName}\" is reserved for the support user";
    public const string InvalidPin = "The PIN must be exactly 4 digits";
    public const string NoSupportUser = "There is no support user yet: run setup first";
}
