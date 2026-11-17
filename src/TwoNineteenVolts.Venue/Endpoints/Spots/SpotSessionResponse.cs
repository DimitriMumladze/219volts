using TwoNineteenVolts.Venue.Application.Spots.Contracts;
using TwoNineteenVolts.Venue.Common;

namespace TwoNineteenVolts.Venue.Endpoints.Spots;

public record SpotSessionResponse(int Id, DateTime StartedAt, DateTime EndsAt, string StartedBy, decimal TotalCharged)
{
    public static SpotSessionResponse From(SpotSessionView view)
    {
        var total = Money.ToGel(view.TotalChargedTetri);
        return new SpotSessionResponse(view.Id, view.StartedAt, view.EndsAt, view.StartedBy, total);
    }
}
