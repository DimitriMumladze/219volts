using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Tests.Auth;

public class AuthEndpointTests
{
    private const string SupportPassword = "support-password-24-char";

    private record LoginUser(int Id, string DisplayName);

    private record CurrentUser(int Id, string DisplayName, string Role);

    [Fact]
    public async Task Users_list_shows_active_users_without_support()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");
        var gio = await TestUsers.AddAsync(factory, "Gio", Role.Staff, "5678");
        await TestUsers.AddAsync(factory, "Old Staff", Role.Staff, "1111", isActive: false);
        await TestUsers.AddAsync(factory, "219volts Support", Role.Support, SupportPassword);
        using var client = factory.CreateClient();

        var users = await client.GetFromJsonAsync<List<LoginUser>>("/api/auth/users");

        Assert.Equal([new LoginUser(gio, "Gio"), new LoginUser(nino, "Nino")], users);
    }

    [Fact]
    public async Task Login_with_right_pin_sets_cookie_and_me_returns_user()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "0123");
        using var client = factory.CreateClient();

        var login = await client.LoginAsync(nino, "0123");

        var cookies = login.Headers.GetValues("Set-Cookie");
        var loggedIn = await login.Content.ReadFromJsonAsync<CurrentUser>();
        var me = await client.GetFromJsonAsync<CurrentUser>("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains(cookies, cookie => cookie.StartsWith("219volts.session="));
        Assert.Equal(new CurrentUser(nino, "Nino", "Manager"), loggedIn);
        Assert.Equal(new CurrentUser(nino, "Nino", "Manager"), me);
    }

    [Fact]
    public async Task Login_with_wrong_pin_returns_401_wrong_pin()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "0123");
        using var client = factory.CreateClient();

        var login = await client.LoginAsync(nino, "0124");

        await AssertWrongPinAsync(login);
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Login_without_pin_returns_400()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "0123");
        using var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { userId = nino });

        Assert.Equal(HttpStatusCode.BadRequest, login.StatusCode);
        var problem = await login.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(400, problem!.Status);
    }

    [Fact]
    public async Task Me_without_login_returns_401_problem()
    {
        using var factory = new VenueAppFactory();
        using var client = factory.CreateClient();

        var me = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        var problem = await me.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(401, problem!.Status);
    }

    [Fact]
    public async Task Login_as_inactive_or_support_user_returns_same_401()
    {
        using var factory = new VenueAppFactory();
        var inactive = await TestUsers.AddAsync(factory, "Old Staff", Role.Staff, "1111", isActive: false);
        var support = await TestUsers.AddAsync(factory, "219volts Support", Role.Support, "2222");
        using var client = factory.CreateClient();

        var inactiveLogin = await client.LoginAsync(inactive, "1111");
        var supportLogin = await client.LoginAsync(support, "2222");
        var unknownLogin = await client.LoginAsync(999, "1234");

        await AssertWrongPinAsync(inactiveLogin);
        await AssertWrongPinAsync(supportLogin);
        await AssertWrongPinAsync(unknownLogin);
    }

    [Fact]
    public async Task Fourth_wrong_pin_then_immediate_retry_returns_429()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "0123");
        using var client = factory.CreateClient();

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var wrongPin = await client.LoginAsync(nino, "9999");
            await AssertWrongPinAsync(wrongPin);
        }

        var retry = await client.LoginAsync(nino, "0123");

        var retryAfter = retry.Headers.GetValues("Retry-After").Single();
        var problem = await retry.Content.ReadFromJsonAsync<ProblemDetails>();
        var code = problem!.Extensions["code"]!.ToString();
        Assert.Equal(HttpStatusCode.TooManyRequests, retry.StatusCode);
        Assert.Equal("1", retryAfter);
        Assert.Equal("Try again in 1 second", problem.Title);
        Assert.Equal("AuthTryAgainInOneSecond", code);

        var oneSecond = TimeSpan.FromSeconds(1);
        factory.Time.Advance(oneSecond);
        var afterWaiting = await client.LoginAsync(nino, "0123");
        Assert.Equal(HttpStatusCode.OK, afterWaiting.StatusCode);
    }

    [Fact]
    public async Task Fifth_wrong_pin_says_try_again_in_2_seconds()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "0123");
        using var client = factory.CreateClient();
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var wrongPin = await client.LoginAsync(nino, "9999");
            await AssertWrongPinAsync(wrongPin);
        }

        var oneSecond = TimeSpan.FromSeconds(1);
        factory.Time.Advance(oneSecond);
        var fifthWrongPin = await client.LoginAsync(nino, "9999");
        await AssertWrongPinAsync(fifthWrongPin);
        var retry = await client.LoginAsync(nino, "0123");

        var problem = await retry.Content.ReadFromJsonAsync<ProblemDetails>();
        var code = problem!.Extensions["code"]!.ToString();
        Assert.Equal(HttpStatusCode.TooManyRequests, retry.StatusCode);
        Assert.Equal("Try again in 2 seconds", problem.Title);
        Assert.Equal("AuthTryAgainInSeconds", code);
    }

    [Fact]
    public async Task Ids_without_a_loginable_user_are_not_tracked_by_the_slow_down()
    {
        using var factory = new VenueAppFactory();
        var inactive = await TestUsers.AddAsync(factory, "Old Staff", Role.Staff, "1111", isActive: false);
        using var client = factory.CreateClient();

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            var unknownLogin = await client.LoginAsync(999, "1234");
            var inactiveLogin = await client.LoginAsync(inactive, "1111");
            await AssertWrongPinAsync(unknownLogin);
            await AssertWrongPinAsync(inactiveLogin);
        }
    }

    [Fact]
    public async Task Support_login_with_password_works_and_wrong_password_401()
    {
        using var factory = new VenueAppFactory();
        var support = await TestUsers.AddAsync(factory, "219volts Support", Role.Support, SupportPassword);
        using var client = factory.CreateClient();

        var wrongPassword = await client.SupportLoginAsync("wrong-password");
        await AssertWrongPinAsync(wrongPassword);

        var login = await client.SupportLoginAsync(SupportPassword);

        var me = await client.GetFromJsonAsync<CurrentUser>("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(new CurrentUser(support, "219volts Support", "Support"), me);
    }

    [Fact]
    public async Task Logout_ends_session()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "0123");
        using var client = factory.CreateClient();
        await client.LoginAsync(nino, "0123");

        var logout = await client.PostAsync("/api/auth/logout", null);

        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Session_idle_longer_than_the_timeout_gets_401_and_activity_keeps_it_alive()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "0123");
        using var client = factory.CreateClient();
        await client.LoginAsync(nino, "0123");

        var fiftyMinutes = TimeSpan.FromMinutes(50);
        var sixtyOneMinutes = TimeSpan.FromMinutes(61);

        factory.Time.Advance(fiftyMinutes);
        var afterFifty = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, afterFifty.StatusCode);
        factory.Time.Advance(fiftyMinutes);
        var afterHundred = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, afterHundred.StatusCode);
        factory.Time.Advance(sixtyOneMinutes);

        var afterIdle = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterIdle.StatusCode);
    }

    [Fact]
    public async Task Login_and_failed_login_are_audited()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "0123");
        using var client = factory.CreateClient();

        await client.LoginAsync(nino, "0124");
        await client.LoginAsync(nino, "0123");
        await client.PostAsync("/api/auth/logout", null);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        var entries = await db.AuditEntries.OrderBy(e => e.Id).ToListAsync();

        Assert.Collection(
            entries,
            failed =>
            {
                Assert.Equal(AuditActions.LoginFailed, failed.Action);
                Assert.Null(failed.ActorUserId);
                Assert.Equal("unknown", failed.ActorName);
                Assert.Equal($"user:{nino}", failed.Target);
            },
            login =>
            {
                Assert.Equal(AuditActions.Login, login.Action);
                Assert.Equal(nino, login.ActorUserId);
                Assert.Equal("Nino", login.ActorName);
            },
            logout =>
            {
                Assert.Equal(AuditActions.Logout, logout.Action);
                Assert.Equal(nino, logout.ActorUserId);
            });
    }


    private static async Task AssertWrongPinAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Wrong PIN", problem!.Title);
        var code = problem.Extensions["code"]!.ToString();
        Assert.Equal("AuthWrongPin", code);
    }
}
