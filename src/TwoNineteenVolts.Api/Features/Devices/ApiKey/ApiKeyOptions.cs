using System.ComponentModel.DataAnnotations;

namespace TwoNineteenVolts.Api.Features.Devices.ApiKey;

public class ApiKeyOptions
{
    public const string SectionName = "ApiKey";

    [Required]
    public string Key { get; set; } = "";
}
