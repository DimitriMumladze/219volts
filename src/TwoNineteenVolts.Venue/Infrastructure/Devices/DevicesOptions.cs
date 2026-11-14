namespace TwoNineteenVolts.Venue.Infrastructure.Devices;

public class DevicesOptions
{
    public const string SectionName = "Devices";

    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(10);
}
