using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Infrastructure.Events;

public static class EventsModule
{
    public static IServiceCollection AddEvents(this IServiceCollection services)
    {
        services.AddSingleton<EventQueue>();
        services.AddSingleton<IEventQueue>(provider => provider.GetRequiredService<EventQueue>());
        services.AddHostedService<EventDeliveryService>();

        return services;
    }
}
