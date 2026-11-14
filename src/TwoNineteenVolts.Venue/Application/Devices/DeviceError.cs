namespace TwoNineteenVolts.Venue.Application.Devices;

// Expected failures of device management and device calls, as keys into Resources/Messages.resx.
public static class DeviceError
{
    public const string NotFound = "DeviceNotFound";
    public const string InvalidName = "DeviceInvalidName";
    public const string DuplicateName = "DeviceDuplicateName";
    public const string InvalidIpAddress = "DeviceInvalidIpAddress";
    public const string InvalidPort = "DeviceInvalidPort";
    public const string InvalidChannelCount = "DeviceInvalidChannelCount";
    public const string DuplicateAddress = "DeviceDuplicateAddress";

    // Timeout, host unreachable or another network failure: counts as a missed check.
    public const string NoAnswer = "DeviceNoAnswer";

    // The device answered, but with an error or something unreadable: it is reachable.
    public const string ErrorAnswer = "DeviceErrorAnswer";
}
