using TwoNineteenVolts.Venue.Application.Devices;

namespace TwoNineteenVolts.Venue.Infrastructure.Devices;

public static class DevicesModule
{
    public static IServiceCollection AddDevices(this IServiceCollection services)
    {
        services.AddOptions<DevicesOptions>()
            .BindConfiguration(DevicesOptions.SectionName);

        // One status store for the whole app: the check and the switches share it.
        services.AddSingleton<DeviceStatuses>();
        services.AddScoped<RelayDrivers>();
        services.AddScoped<DeviceService>();
        services.AddHostedService<DeviceCheckService>();

        return services;
    }
}
