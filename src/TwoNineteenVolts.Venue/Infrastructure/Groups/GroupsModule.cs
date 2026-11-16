using TwoNineteenVolts.Venue.Application.Groups;

namespace TwoNineteenVolts.Venue.Infrastructure.Groups;

public static class GroupsModule
{
    public static IServiceCollection AddGroups(this IServiceCollection services)
    {
        services.AddScoped<GroupService>();

        return services;
    }
}
