using System.Globalization;

namespace TwoNineteenVolts.Venue.Common;

public static class Money
{
    private const decimal TetriPerGel = 100m;
    private const decimal GelPerTetri = 0.01m;
    private const int Decimals = 2;
    private const long MinutesPerHour = 60;
    private const long HalfAnHour = 30;

    public static long ToTetri(decimal gel)
    {
        var tetri = gel * TetriPerGel;
        return (long)tetri;
    }

    public static decimal ToGel(long tetri)
    {
        return tetri * GelPerTetri;
    }

    public static bool HasAtMostTwoDecimals(decimal gel)
    {
        var rounded = decimal.Round(gel, Decimals);
        return rounded == gel;
    }

    public static long PriceTetri(int minutes, long hourlyRateTetri)
    {
        var minuteTetri = minutes * hourlyRateTetri;
        return (minuteTetri + HalfAnHour) / MinutesPerHour;
    }

    public static string Show(long tetri)
    {
        var gel = ToGel(tetri);
        return gel.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
