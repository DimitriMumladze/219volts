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
        Assert.True(hasher.Verify(hash, "0123"));
        Assert.False(hasher.Verify(hash, "0124"));
    }
}
