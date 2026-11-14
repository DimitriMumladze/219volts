using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TwoNineteenVolts.Venue.Application.Auth;

namespace TwoNineteenVolts.Venue.Infrastructure.Auth;

// Keeps the identity of each signed-in session in memory; the cookie holds only the random key.
// A restart therefore signs everybody out, which the spec accepts.
// The cookie handler enforces the inactivity timeout itself and removes an expired session here
// when its cookie comes back.
public class InMemoryTicketStore : ITicketStore, ISessionRegistry
{
    private const int KeySizeBytes = 32;

    private readonly ConcurrentDictionary<string, AuthenticationTicket> _sessions = new();

    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(KeySizeBytes));
        _sessions[key] = ticket;
        return Task.FromResult(key);
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        // Only renew a session that still exists; an ended session must not come back.
        // TryUpdate writes only if the entry is still the one we read, so a session removed
        // in the meantime (logout, EndSessionsFor) stays removed.
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

    private static int UserIdOf(AuthenticationTicket ticket) =>
        int.Parse(ticket.Principal.FindFirst(AuthClaims.UserId)!.Value);
}
