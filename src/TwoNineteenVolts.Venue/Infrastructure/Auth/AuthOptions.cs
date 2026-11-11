using System.ComponentModel.DataAnnotations;

namespace TwoNineteenVolts.Venue.Infrastructure.Auth;

public class AuthOptions
{
    public const string SectionName = "Auth";

    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan InactivityTimeout { get; set; } = TimeSpan.FromHours(1);
}
