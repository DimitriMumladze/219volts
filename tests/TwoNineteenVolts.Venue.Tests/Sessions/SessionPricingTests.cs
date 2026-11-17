using System.Globalization;
using TwoNineteenVolts.Venue.Application.Sessions;

namespace TwoNineteenVolts.Venue.Tests.Sessions;

public class SessionPricingTests
{
    [Fact]
    public void Price_is_rounded_half_up()
    {
        var price = SessionPricing.PriceTetri(7, 1000);

        Assert.Equal(117, price);
    }

    [Fact]
    public void No_amount_means_the_price()
    {
        var charged = SessionPricing.ChargedTetri(null, 1000);

        Assert.Equal(1000, charged.Value);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("10.01")]
    [InlineData("1.005")]
    public void Amount_outside_0_to_price_or_with_3_decimals_is_invalid(string amountText)
    {
        var amount = decimal.Parse(amountText, CultureInfo.InvariantCulture);

        var charged = SessionPricing.ChargedTetri(amount, 1000);

        Assert.Equal(SessionError.InvalidAmount, charged.Error);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("5.00", 500)]
    [InlineData("10", 1000)]
    public void Zero_and_lower_amounts_are_kept(string amountText, long expected)
    {
        var amount = decimal.Parse(amountText, CultureInfo.InvariantCulture);

        var charged = SessionPricing.ChargedTetri(amount, 1000);

        Assert.Equal(expected, charged.Value);
    }

    [Fact]
    public void Details_show_the_change_only_when_lowered()
    {
        var full = SessionPricing.Details(60, 1000, 1000);
        var lowered = SessionPricing.Details(60, 1000, 500);

        Assert.Equal("60 min, 10.00", full);
        Assert.Equal("60 min, 10.00 → 5.00", lowered);
    }
}
