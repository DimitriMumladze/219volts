using TwoNineteenVolts.Venue.Application.Events;
using TwoNineteenVolts.Venue.Application.Sessions;

namespace TwoNineteenVolts.Venue.Infrastructure.Sessions;

public static class SessionsModule
{
    public static IServiceCollection AddSessions(this IServiceCollection services)
    {
        services.AddSingleton<SwitchOffWarnings>();
        services.AddScoped<SessionService>();
        services.AddScoped<IRunningSessions, RunningSessions>();
        services.AddScoped<IEventListener, SwitchOffWarningCleaner>();
        services.AddHostedService<SessionTickService>();

        return services;
    }
}
