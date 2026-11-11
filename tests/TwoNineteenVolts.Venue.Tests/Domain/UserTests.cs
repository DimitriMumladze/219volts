using TwoNineteenVolts.Venue.Domain.Auth;

namespace TwoNineteenVolts.Venue.Tests.Domain;

public class UserTests
{
    private static readonly DateTime CreatedAt = new(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_trims_display_name()
    {
        var user = new User("  Nino ", Role.Staff, "hash", CreatedAt);

        Assert.Equal("Nino", user.DisplayName);
    }

    [Fact]
    public void Constructor_rejects_empty_name()
    {
        Assert.Throws<ArgumentException>(() => new User("   ", Role.Staff, "hash", CreatedAt));
    }

    [Fact]
    public void Deactivate_then_reactivate()
    {
        var user = new User("Nino", Role.Staff, "hash", CreatedAt);

        user.Deactivate();
        Assert.False(user.IsActive);

        user.Reactivate();
        Assert.True(user.IsActive);
    }

    [Fact]
    public void New_user_has_remote_access_off()
    {
        var user = new User("Nino", Role.Staff, "hash", CreatedAt);

        Assert.False(user.RemoteAccessAllowed);
    }
}
