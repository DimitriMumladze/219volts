using TwoNineteenVolts.Venue.Application.Spots;
using TwoNineteenVolts.Venue.Application.Spots.Contracts;

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
