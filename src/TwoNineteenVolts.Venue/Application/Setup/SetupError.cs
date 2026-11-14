namespace TwoNineteenVolts.Venue.Application.Setup;

// Expected failures of installing a venue and resetting the Support password,
// as keys into Resources/Messages.resx.
public static class SetupError
{
    public const string AlreadyDone = "SetupAlreadyDone";
    public const string InvalidName = "SetupInvalidName";
    public const string ReservedName = "SetupReservedName";
    public const string InvalidPin = "SetupInvalidPin";
    public const string NoSupportUser = "SetupNoSupportUser";
}
