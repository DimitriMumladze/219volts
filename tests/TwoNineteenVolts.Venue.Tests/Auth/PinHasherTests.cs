using TwoNineteenVolts.Venue.Infrastructure.Auth;

namespace TwoNineteenVolts.Venue.Tests.Auth;

public class PinHasherTests
{
    [Fact]
    public void Hash_then_verify()
    {
        var hasher = new PinHasher();

        var hash = hasher.Hash("0123");

        Assert.NotEqual("0123", hash);
        var isRightPinMatch = hasher.IsMatch(hash, "0123");
        var isWrongPinMatch = hasher.IsMatch(hash, "0124");
        Assert.True(isRightPinMatch);
        Assert.False(isWrongPinMatch);
    }
}
