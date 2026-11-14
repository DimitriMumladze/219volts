namespace TwoNineteenVolts.Venue.Commands.Setup;

// What the setup and reset-support commands print, as keys into Resources/Messages.resx.
public static class SetupMessages
{
    public const string OwnerNamePrompt = "SetupOwnerNamePrompt";
    public const string OwnerPinPrompt = "SetupOwnerPinPrompt";
    public const string PinAgainPrompt = "SetupPinAgainPrompt";
    public const string PinsDoNotMatch = "SetupPinsDoNotMatch";

    // {0} = the owner's name.
    public const string Created = "SetupCreated";
    public const string SupportPasswordIntro = "SetupSupportPasswordIntro";

    // {0} = the reason the command stopped.
    public const string NothingChanged = "SetupNothingChanged";
    public const string NotAdministrator = "SetupNotAdministrator";
    public const string NewSupportPasswordIntro = "SetupNewSupportPasswordIntro";
    public const string RestartService = "SetupRestartService";
}
