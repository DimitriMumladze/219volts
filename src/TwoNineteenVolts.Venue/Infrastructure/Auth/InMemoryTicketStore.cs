using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TwoNineteenVolts.Venue.Application.Auth;

namespace TwoNineteenVolts.Venue.Infrastructure.Auth;

public class InMemoryTicketStore : ITicketStore, ISessionRegistry
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
        _sessions.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public void EndSessionsFor(int userId)
    {
        foreach (var session in _sessions)
        {
            if (UserIdOf(session.Value) == userId)
            {
                _sessions.TryRemove(session.Key, out _);
            }
        }
    }


    private static int UserIdOf(AuthenticationTicket ticket)
    {
        var idClaim = ticket.Principal.FindFirst(AuthClaims.UserId)!.Value;
        return int.Parse(idClaim);
    }
}
