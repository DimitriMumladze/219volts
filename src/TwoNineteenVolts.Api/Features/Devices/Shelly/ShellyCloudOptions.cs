using System.ComponentModel.DataAnnotations;

namespace TwoNineteenVolts.Api.Features.Devices.Shelly;

public class ShellyCloudOptions
{
    public const string SectionName = "ShellyCloud";

    [Required]
    public string ServerUrl { get; set; } = "";

    [Required]
    public string AuthKey { get; set; } = "";
}
