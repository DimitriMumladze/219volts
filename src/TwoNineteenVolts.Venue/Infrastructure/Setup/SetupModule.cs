using TwoNineteenVolts.Venue.Application.Setup;

namespace TwoNineteenVolts.Venue.Infrastructure.Setup;

public static class SetupModule
{
    public static IServiceCollection AddSetup(this IServiceCollection services)
    {
        services.AddScoped<SetupService>();
        services.AddScoped<ResetSupportService>();

        return services;
    }
}
