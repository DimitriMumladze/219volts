using TwoNineteenVolts.Venue.Application.Spots;

namespace TwoNineteenVolts.Venue.Infrastructure.Spots;

public static class SpotsModule
{
    public static IServiceCollection AddSpots(this IServiceCollection services)
    {
        services.AddSingleton<ISpotCodeGenerator, SpotCodeGenerator>();
        services.AddScoped<SpotService>();
        services.AddScoped<SpotReader>();
        services.AddScoped<SwitchFailures>();

        return services;
    }
}
