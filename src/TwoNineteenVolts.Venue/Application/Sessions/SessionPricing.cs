using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Common;

namespace TwoNineteenVolts.Venue.Application.Sessions;

public static class SessionPricing
{
    public static long PriceTetri(int minutes, long hourlyRateTetri)
    {
        return Money.PriceTetri(minutes, hourlyRateTetri);
    }

    public static long? ChargedTetri(decimal? amount, long computedTetri)
    {
        if (amount is not decimal typed)
        {
            return computedTetri;
        }

        var computedGel = Money.ToGel(computedTetri);
        if (typed < 0 || typed > computedGel)
        {
            return null;
        }

        if (!Money.HasAtMostTwoDecimals(typed))
        {
            return null;
        }

        return Money.ToTetri(typed);
    }

    public static string Details(int minutes, long computedTetri, long chargedTetri)
    {
        var computed = Money.Show(computedTetri);
        if (chargedTetri == computedTetri)
        {
            return $"{minutes} min, {computed}";
        }

        var charged = Money.Show(chargedTetri);
        var change = AuditChanges.Change(computed, charged);
        return $"{minutes} min, {change}";
    }
}
