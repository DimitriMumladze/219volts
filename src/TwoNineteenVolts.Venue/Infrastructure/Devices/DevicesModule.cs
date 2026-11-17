using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Application.Devices.Contracts;

namespace TwoNineteenVolts.Venue.Infrastructure.Devices;

public static class DevicesModule
{
    public static IServiceCollection AddDevices(this IServiceCollection services)
    {
        services.AddOptions<DevicesOptions>()
            .BindConfiguration(DevicesOptions.SectionName)
            .Validate(options => options.CheckInterval > TimeSpan.Zero, "Devices:CheckInterval must be greater than zero")
            .ValidateOnStart();

        services.AddSingleton<DeviceStatuses>();
        services.AddSingleton<DeviceLocks>();
        services.AddScoped<DeviceGateway>();
        services.AddScoped<IDeviceGateway>(provider => provider.GetRequiredService<DeviceGateway>());
        services.AddScoped<RelayDrivers>();
        services.AddScoped<DeviceService>();
        services.AddHttpClient<ShellyRelayDriver>();
        services.AddTransient<IRelayDriver>(provider => provider.GetRequiredService<ShellyRelayDriver>());
        services.AddSingleton<IRelayDriver, ModbusRelayDriver>();
        services.AddHostedService<DeviceCheckService>();

        return services;
    }
}
