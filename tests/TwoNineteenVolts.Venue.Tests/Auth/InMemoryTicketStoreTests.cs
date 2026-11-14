using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using TwoNineteenVolts.Venue.Infrastructure.Auth;

namespace TwoNineteenVolts.Venue.Tests.Auth;

public class InMemoryTicketStoreTests
{
    [Fact]
    public async Task Store_then_retrieve_returns_ticket()
    {
        var store = new InMemoryTicketStore();
        var ticket = TicketFor(7, "Nino");

        var key = await store.StoreAsync(ticket);
        var retrieved = await store.RetrieveAsync(key);

        Assert.Same(ticket, retrieved);
    }

    [Fact]
    public async Task Each_session_gets_its_own_key()
    {
        var store = new InMemoryTicketStore();

        var first = await store.StoreAsync(TicketFor(7, "Nino"));
        var second = await store.StoreAsync(TicketFor(7, "Nino"));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task EndSessionsFor_removes_all_sessions_of_that_user_only()
    {
        var store = new InMemoryTicketStore();
        var ninoTablet = await store.StoreAsync(TicketFor(7, "Nino"));
        var ninoCounter = await store.StoreAsync(TicketFor(7, "Nino"));
        var gio = await store.StoreAsync(TicketFor(8, "Gio"));

        store.EndSessionsFor(7);

        Assert.Null(await store.RetrieveAsync(ninoTablet));
        Assert.Null(await store.RetrieveAsync(ninoCounter));
        Assert.NotNull(await store.RetrieveAsync(gio));
    }

    [Fact]
    public async Task Remove_ends_the_session()
    {
        var store = new InMemoryTicketStore();
        var key = await store.StoreAsync(TicketFor(7, "Nino"));

        await store.RemoveAsync(key);

        Assert.Null(await store.RetrieveAsync(key));
    }

    [Fact]
    public async Task Renew_does_not_bring_back_an_ended_session()
    {
        var store = new InMemoryTicketStore();
        var ticket = TicketFor(7, "Nino");
        var key = await store.StoreAsync(ticket);
        store.EndSessionsFor(7);

        await store.RenewAsync(key, ticket);

        Assert.Null(await store.RetrieveAsync(key));
    }

    [Fact]
    public async Task Renew_replaces_the_ticket_of_an_existing_session()
    {
        var store = new InMemoryTicketStore();
        var key = await store.StoreAsync(TicketFor(7, "Nino"));
        var renewed = TicketFor(7, "Nino");

        await store.RenewAsync(key, renewed);

        Assert.Same(renewed, await store.RetrieveAsync(key));
    }

    private static AuthenticationTicket TicketFor(int userId, string name)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(AuthClaims.UserId, userId.ToString()),
                new Claim(AuthClaims.Name, name)
            ],
            "test");

        return new AuthenticationTicket(new ClaimsPrincipal(identity), "test");
    }
}
