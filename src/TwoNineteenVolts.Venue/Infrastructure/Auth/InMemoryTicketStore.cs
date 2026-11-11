using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TwoNineteenVolts.Venue.Application.Auth;

namespace TwoNineteenVolts.Venue.Infrastructure.Auth;

// Keeps the identity of each signed-in session in memory; the cookie holds only the random key.
// A restart therefore signs everybody out, which the spec accepts.
public class InMemoryTicketStore(TimeProvider time) : ITicketStore, ISessionRegistry
{
    private const int KeySizeBytes = 32;

    private readonly ConcurrentDictionary<string, (AuthenticationTicket Ticket, DateTimeOffset LastActivity)> _sessions = new();

    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(KeySizeBytes));
        _sessions[key] = (ticket, time.GetUtcNow());
        return Task.FromResult(key);
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        // Only renew a session that still exists; an ended session must not come back.
        if (_sessions.ContainsKey(key))
        {
            _sessions[key] = (ticket, time.GetUtcNow());
        }

        return Task.CompletedTask;
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        if (!_sessions.TryGetValue(key, out var session))
        {
            return Task.FromResult<AuthenticationTicket?>(null);
        }

        _sessions.TryUpdate(key, (session.Ticket, time.GetUtcNow()), session);
        return Task.FromResult<AuthenticationTicket?>(session.Ticket);
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
            if (UserIdOf(session.Value.Ticket) == userId)
            {
                _sessions.TryRemove(session.Key, out _);
            }
        }
    }

    public IReadOnlyList<SessionInfo> RemoveIdleSessions(TimeSpan inactivityTimeout)
    {
        var now = time.GetUtcNow();
        var removed = new List<SessionInfo>();

        foreach (var session in _sessions)
        {
            var (ticket, lastActivity) = session.Value;

            // Removing by key and value skips a session that was used again since we read it.
            if (now - lastActivity > inactivityTimeout && _sessions.TryRemove(session))
            {
                removed.Add(new SessionInfo(session.Key, UserIdOf(ticket), NameOf(ticket), lastActivity));
            }
        }

        return removed;
    }

    private static int UserIdOf(AuthenticationTicket ticket) =>
        int.Parse(ticket.Principal.FindFirst(AuthClaims.UserId)!.Value);

    private static string NameOf(AuthenticationTicket ticket) =>
        ticket.Principal.FindFirst(AuthClaims.Name)!.Value;
}
