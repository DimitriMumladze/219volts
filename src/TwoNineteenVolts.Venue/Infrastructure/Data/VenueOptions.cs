using System.ComponentModel.DataAnnotations;

namespace TwoNineteenVolts.Venue.Infrastructure.Data;

public class VenueOptions
{
    public const string SectionName = "Venue";

    [Required]
    public string DataFolder { get; set; } = "";
}
