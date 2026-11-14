using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Auth;
using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Infrastructure.Auth;

public static class AuthModule
{
    private const string CookieName = "219volts.session";

    public static IServiceCollection AddAuth(this IServiceCollection services)
    {
        services.AddOptions<AuthOptions>()
            .BindConfiguration(AuthOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        AddSessionCookie(services);
        AddRolePolicies(services);
        AddLoginServices(services);

        return services;
    }


    private static void AddSessionCookie(IServiceCollection services)
    {
        services.AddSingleton<InMemoryTicketStore>();
        services.AddSingleton<ISessionRegistry>(provider => provider.GetRequiredService<InMemoryTicketStore>());

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = CookieSecurePolicy.None;
                options.SlidingExpiration = true;

                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthOptions>, InMemoryTicketStore>((options, auth, store) =>
            {
                options.ExpireTimeSpan = auth.Value.InactivityTimeout;
                options.SessionStore = store;
            });
    }

    private static void AddRolePolicies(IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Staff, policy => policy.RequireClaim(AuthClaims.Role, nameof(Role.Staff), nameof(Role.Manager), nameof(Role.Support)))
            .AddPolicy(Policies.Manager, policy => policy.RequireClaim(AuthClaims.Role, nameof(Role.Manager), nameof(Role.Support)))
            .AddPolicy(Policies.Support, policy => policy.RequireClaim(AuthClaims.Role, nameof(Role.Support)));
    }

    private static void AddLoginServices(IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentActor, CurrentActor>();
        services.AddSingleton<IPinHasher, PinHasher>();
        services.AddSingleton<LoginThrottle>();
        services.AddScoped<LoginService>();
    }
}
