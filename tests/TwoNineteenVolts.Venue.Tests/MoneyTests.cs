using TwoNineteenVolts.Venue.Common;

namespace TwoNineteenVolts.Venue.Tests;

public class MoneyTests
{
    [Theory]
    [InlineData(7, 1000, 117)]
    [InlineData(60, 1000, 1000)]
    [InlineData(1, 1, 0)]
    [InlineData(600, 999999, 9999990)]
    [InlineData(3, 1000, 50)]
    public void Price_is_minutes_over_60_times_rate_rounded_half_up(int minutes, long hourlyRateTetri, long expectedTetri)
    {
        var price = Money.PriceTetri(minutes, hourlyRateTetri);

        Assert.Equal(expectedTetri, price);
    }

    [Fact]
    public void Half_a_tetri_rounds_up()
    {
        var price = Money.PriceTetri(1, 30);

        Assert.Equal(1, price);
    }

    [Theory]
    [InlineData("12.34", 1234)]
    [InlineData("0.01", 1)]
    [InlineData("9999.99", 999999)]
    public void Gel_converts_to_tetri_and_back(string gelText, long tetri)
    {
        var gel = decimal.Parse(gelText, System.Globalization.CultureInfo.InvariantCulture);

        var toTetri = Money.ToTetri(gel);
        var back = Money.ToGel(tetri);

        Assert.Equal(tetri, toTetri);
        Assert.Equal(gel, back);
    }

    [Theory]
    [InlineData("1.00", true)]
    [InlineData("1.5", true)]
    [InlineData("1.005", false)]
    [InlineData("0.001", false)]
    public void At_most_two_decimals(string gelText, bool expected)
    {
        var gel = decimal.Parse(gelText, System.Globalization.CultureInfo.InvariantCulture);

        var result = Money.HasAtMostTwoDecimals(gel);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Huge_amount_does_not_crash_the_decimal_check()
    {
        var huge = decimal.MaxValue;

        var result = Money.HasAtMostTwoDecimals(huge);

        Assert.True(result);
    }

    [Theory]
    [InlineData(1000, "10.00")]
    [InlineData(1050, "10.50")]
    [InlineData(1, "0.01")]
    public void Gel_always_has_two_decimals(long tetri, string expected)
    {
        var gel = Money.ToGel(tetri);

        var text = gel.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(expected, text);
    }

    [Fact]
    public void Gel_text_has_two_decimals()
    {
        var text = Money.Show(1250);

        Assert.Equal("12.50", text);
    }
}
