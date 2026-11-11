using TwoNineteenVolts.Venue.Infrastructure.Auth;

namespace TwoNineteenVolts.Venue.Tests.Auth;

public class SecretGeneratorTests
{
    [Fact]
    public void Support_password_is_24_unambiguous_letters_and_digits()
    {
        var password = SecretGenerator.NewSupportPassword();

        Assert.Equal(24, password.Length);
        Assert.All(password, c => Assert.True(char.IsAsciiLetterOrDigit(c)));
        Assert.DoesNotContain(password, c => "0O1lI".Contains(c));
    }

    [Fact]
    public void Each_password_is_different()
    {
        Assert.NotEqual(SecretGenerator.NewSupportPassword(), SecretGenerator.NewSupportPassword());
    }
}
