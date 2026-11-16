using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Endpoints.Live;

public static class LiveEndpoints
{
    public static IServiceCollection AddLive(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddSingleton<HubConnections>();
        services.AddScoped<IEventListener, SpotChangedPusher>();
        services.AddScoped<IEventListener, LoginEndedCloser>();

        return services;
    }

    public static void MapLiveHub(this IEndpointRouteBuilder app)
    {
        app.MapHub<VenueHub>("/hubs/venue", options => options.CloseOnAuthenticationExpiration = true);
    }
}
