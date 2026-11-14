using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Tests.Domain;

public class UserTests
{
    private static readonly DateTime CreatedAt = new(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Deactivate_then_reactivate()
    {
        var user = new User("Nino", Role.Staff, "hash", CreatedAt);

        user.Deactivate();
        Assert.False(user.IsActive);

        user.Reactivate();
        Assert.True(user.IsActive);
    }
}
