using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Common;

namespace TwoNineteenVolts.Venue.Application.Sessions;

public static class SessionPricing
{
    public static long PriceTetri(int minutes, long hourlyRateTetri)
    {
        return Money.PriceTetri(minutes, hourlyRateTetri);
    }

    public static Result<long> ChargedTetri(decimal? amount, long computedTetri)
    {
        if (amount is not decimal typedGel)
        {
            return Result<long>.Ok(computedTetri);
        }

        var computedGel = Money.ToGel(computedTetri);
        var isInRange = typedGel >= 0 && typedGel <= computedGel;
        if (!isInRange)
        {
            return Result<long>.Fail(SessionError.InvalidAmount);
        }

        if (!Money.HasAtMostTwoDecimals(typedGel))
        {
            return Result<long>.Fail(SessionError.InvalidAmount);
        }

        var typedTetri = Money.ToTetri(typedGel);
        return Result<long>.Ok(typedTetri);
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
