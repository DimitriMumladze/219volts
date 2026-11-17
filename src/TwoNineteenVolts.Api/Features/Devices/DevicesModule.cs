using TwoNineteenVolts.Api.Features.Devices.ApiKey;
using TwoNineteenVolts.Api.Features.Devices.Registry;
using TwoNineteenVolts.Api.Features.Devices.Shelly;

namespace TwoNineteenVolts.Api.Features.Devices;

public static class DevicesModule
{
    public static IServiceCollection AddDevices(this IServiceCollection services)
    {
        services.AddOptions<ShellyCloudOptions>()
            .BindConfiguration(ShellyCloudOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<ApiKeyOptions>()
            .BindConfiguration(ApiKeyOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<ShellyCloudClient>();
        services.AddSingleton<DeviceRegistry>();

        return services;
    }
}
