using TwoNineteenVolts.Venue.Application.Auth;

namespace TwoNineteenVolts.Venue.Tests.Auth;

public class PinFormatTests
{
    [Theory]
    [InlineData("0123", true)]
    [InlineData("123", false)]
    [InlineData("12345", false)]
    [InlineData("12a4", false)]
    [InlineData(" 123", false)]
    [InlineData("١٢٣٤", false)]
    public void Pin_format(string pin, bool isExpectedValid)
    {
        var isValid = PinFormat.IsValid(pin);
        Assert.Equal(isExpectedValid, isValid);
    }
}
