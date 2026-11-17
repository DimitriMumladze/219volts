using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TwoNineteenVolts.Venue.Application.Auth;
using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Infrastructure.Auth;

public class InMemoryTicketStore(IEventQueue events) : ITicketStore, ISessionRegistry
{
    private const int KeySizeBytes = 32;

    private readonly ConcurrentDictionary<string, AuthenticationTicket> _sessions = new();

    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var keyBytes = RandomNumberGenerator.GetBytes(KeySizeBytes);
        var key = Convert.ToHexString(keyBytes);
        _sessions[key] = ticket;
        return Task.FromResult(key);
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        if (_sessions.TryGetValue(key, out var current))
        {
            _sessions.TryUpdate(key, ticket, current);
        }

        return Task.CompletedTask;
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        _sessions.TryGetValue(key, out var ticket);
        return Task.FromResult(ticket);
    }

    public Task RemoveAsync(string key)
    {
        if (_sessions.TryRemove(key, out var ticket))
        {
            var userId = UserIdOf(ticket);
            events.Publish(new LoginEnded(userId));
        }

        return Task.CompletedTask;
    }

    public void EndSessionsFor(int userId)
    {
        var hasEndedAny = false;
        foreach (var session in _sessions)
        {
            if (UserIdOf(session.Value) == userId && _sessions.TryRemove(session.Key, out _))
            {
                hasEndedAny = true;
            }
        }

        if (hasEndedAny)
        {
            events.Publish(new LoginEnded(userId));
        }
    }


    private static int UserIdOf(AuthenticationTicket ticket)
    {
        var idClaim = ticket.Principal.FindFirst(AuthClaims.UserId)!.Value;
        return int.Parse(idClaim);
    }
}
