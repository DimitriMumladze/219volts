using Microsoft.AspNetCore.SignalR;

namespace TwoNineteenVolts.Venue.Endpoints.Live;

public class HubConnections
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Entry> _connections = [];

    public void Add(int userId, HubCallerContext context)
    {
        lock (_lock)
        {
            _connections[context.ConnectionId] = new Entry(userId, context);
        }
    }

    public void Remove(string connectionId)
    {
        lock (_lock)
        {
            _connections.Remove(connectionId);
        }
    }

    public void CloseAllFor(int userId)
    {
        List<HubCallerContext> toClose;
        lock (_lock)
        {
            toClose = _connections.Values
                .Where(e => e.UserId == userId)
                .Select(e => e.Context)
                .ToList();
        }

        foreach (var context in toClose)
        {
            context.Abort();
        }
    }


    private record Entry(int UserId, HubCallerContext Context);
}
