using TwoNineteenVolts.Venue.Application.Users;

namespace TwoNineteenVolts.Venue.Infrastructure.Users;

public static class UsersModule
{
    public static IServiceCollection AddUsers(this IServiceCollection services)
    {
        services.AddScoped<UserService>();

        return services;
    }
}
