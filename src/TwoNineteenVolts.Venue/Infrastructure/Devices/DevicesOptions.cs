namespace TwoNineteenVolts.Venue.Infrastructure.Devices;

public class DevicesOptions
{
    public const string SectionName = "Devices";

    // How often every device is checked. No limits on purpose (Q42).
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(10);
}
