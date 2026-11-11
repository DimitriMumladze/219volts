using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TwoNineteenVolts.Venue.Application.Auth;
using TwoNineteenVolts.Venue.Infrastructure.Auth;

namespace TwoNineteenVolts.Venue.Tests.Auth;

public class AuthModuleTests
{
    [Fact]
    public void Session_cookie_is_configured_per_spec()
    {
        using var factory = new VenueAppFactory();

        var cookie = factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        Assert.Equal("219volts.session", cookie.Cookie.Name);
        Assert.True(cookie.Cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Strict, cookie.Cookie.SameSite);
        Assert.Equal(CookieSecurePolicy.None, cookie.Cookie.SecurePolicy);
        Assert.True(cookie.SlidingExpiration);
        Assert.Equal(TimeSpan.FromHours(1), cookie.ExpireTimeSpan);
        Assert.IsType<InMemoryTicketStore>(cookie.SessionStore);
    }

    [Fact]
    public void Session_registry_is_the_same_store_the_cookie_uses()
    {
        using var factory = new VenueAppFactory();

        var cookie = factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        Assert.Same(cookie.SessionStore, factory.Services.GetRequiredService<ISessionRegistry>());
    }
}
