using TwoNineteenVolts.Venue.Application.Setup;

namespace TwoNineteenVolts.Venue.Tests.Setup;

public class SecretGeneratorTests
{
    [Fact]
    public void Support_password_is_24_unambiguous_letters_and_digits()
    {
        var password = SecretGenerator.NewSupportPassword();

        Assert.Equal(24, password.Length);
        var onlyLettersAndDigits = password.All(char.IsAsciiLetterOrDigit);
        Assert.True(onlyLettersAndDigits);
        Assert.DoesNotContain(password, c => "0O1lI".Contains(c));
    }

    [Fact]
    public void Each_password_is_different()
    {
        var first = SecretGenerator.NewSupportPassword();
        var second = SecretGenerator.NewSupportPassword();
        Assert.NotEqual(first, second);
    }
}
