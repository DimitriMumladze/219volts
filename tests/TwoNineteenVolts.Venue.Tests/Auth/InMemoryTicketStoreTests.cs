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

        var firstTicket = TicketFor(7, "Nino");
        var secondTicket = TicketFor(7, "Nino");
        var first = await store.StoreAsync(firstTicket);
        var second = await store.StoreAsync(secondTicket);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task EndSessionsFor_removes_all_sessions_of_that_user_only()
    {
        var store = new InMemoryTicketStore();
        var ninoTabletTicket = TicketFor(7, "Nino");
        var ninoCounterTicket = TicketFor(7, "Nino");
        var gioTicket = TicketFor(8, "Gio");
        var ninoTablet = await store.StoreAsync(ninoTabletTicket);
        var ninoCounter = await store.StoreAsync(ninoCounterTicket);
        var gio = await store.StoreAsync(gioTicket);

        store.EndSessionsFor(7);

        var ninoTabletSession = await store.RetrieveAsync(ninoTablet);
        var ninoCounterSession = await store.RetrieveAsync(ninoCounter);
        var gioSession = await store.RetrieveAsync(gio);
        Assert.Null(ninoTabletSession);
        Assert.Null(ninoCounterSession);
        Assert.NotNull(gioSession);
    }

    [Fact]
    public async Task Remove_ends_the_session()
    {
        var store = new InMemoryTicketStore();
        var ticket = TicketFor(7, "Nino");
        var key = await store.StoreAsync(ticket);

        await store.RemoveAsync(key);

        var session = await store.RetrieveAsync(key);
        Assert.Null(session);
    }

    [Fact]
    public async Task Renew_does_not_bring_back_an_ended_session()
    {
        var store = new InMemoryTicketStore();
        var ticket = TicketFor(7, "Nino");
        var key = await store.StoreAsync(ticket);
        store.EndSessionsFor(7);

        await store.RenewAsync(key, ticket);

        var session = await store.RetrieveAsync(key);
        Assert.Null(session);
    }

    [Fact]
    public async Task Renew_replaces_the_ticket_of_an_existing_session()
    {
        var store = new InMemoryTicketStore();
        var original = TicketFor(7, "Nino");
        var key = await store.StoreAsync(original);
        var renewed = TicketFor(7, "Nino");

        await store.RenewAsync(key, renewed);

        var session = await store.RetrieveAsync(key);
        Assert.Same(renewed, session);
    }


    private static AuthenticationTicket TicketFor(int userId, string name)
    {
        var id = userId.ToString();
        var identity = new ClaimsIdentity(
            [
                new Claim(AuthClaims.UserId, id),
                new Claim(AuthClaims.Name, name)
            ],
            "test");

        var principal = new ClaimsPrincipal(identity);
        return new AuthenticationTicket(principal, "test");
    }
}
