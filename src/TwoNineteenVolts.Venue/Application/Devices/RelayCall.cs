namespace TwoNineteenVolts.Venue.Application.Devices;

public static class RelayCall
{
    // How long any call to a device may take: the check and every switch use it.
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);
}
