using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TwoNineteenVolts.Venue.Infrastructure.Auth;

namespace TwoNineteenVolts.Venue.Endpoints.Live;

[Authorize(Policy = Policies.Staff)]
public class VenueHub(HubConnections connections) : Hub
{
    public const string SpotChanged = "SpotChanged";

    public override Task OnConnectedAsync()
    {
        var idClaim = Context.User!.FindFirst(AuthClaims.UserId)!.Value;
        var userId = int.Parse(idClaim);
        connections.Add(userId, Context);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
