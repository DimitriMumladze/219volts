using TwoNineteenVolts.Venue.Application.Devices;

namespace TwoNineteenVolts.Venue.Infrastructure.Devices;

public static class DevicesModule
{
    public static IServiceCollection AddDevices(this IServiceCollection services)
    {
        services.AddOptions<DevicesOptions>()
            .BindConfiguration(DevicesOptions.SectionName);

        services.AddSingleton<DeviceStatuses>();
        services.AddScoped<RelayDrivers>();
        services.AddScoped<DeviceService>();
        services.AddHttpClient<ShellyRelayDriver>();
        services.AddTransient<IRelayDriver>(provider => provider.GetRequiredService<ShellyRelayDriver>());
        services.AddSingleton<IRelayDriver, ModbusRelayDriver>();
        services.AddHostedService<DeviceCheckService>();

        return services;
    }
}
