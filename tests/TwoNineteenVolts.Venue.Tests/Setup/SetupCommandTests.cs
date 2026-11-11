using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Application.Auth;
using TwoNineteenVolts.Venue.Commands.Setup;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Tests.Setup;

public class SetupCommandTests
{
    [Fact]
    public async Task Setup_creates_manager_and_support_and_prints_password_once()
    {
        using var factory = new VenueAppFactory();
        var console = new FakeConsole("  Nino ", "0123", "0123");

        var exitCode = await RunAsync(factory, console);

        Assert.Equal(0, exitCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPinHasher>();
        var users = await db.Users.OrderBy(u => u.Id).ToListAsync();

        var owner = users[0];
        Assert.Equal(("Nino", Role.Manager, true), (owner.DisplayName, owner.Role, owner.IsActive));
        Assert.True(hasher.Verify(owner.SecretHash, "0123"));

        var support = users[1];
        Assert.Equal(("219volts Support", Role.Support), (support.DisplayName, support.Role));
        var printed = Assert.Single(console.Output, line => line.Length == 24 && hasher.Verify(support.SecretHash, line));
        Assert.Single(console.Output, line => line.Contains(printed));

        var entries = await db.AuditEntries.OrderBy(e => e.Id).ToListAsync();
        Assert.Equal([$"user:{owner.Id}", $"user:{support.Id}"], entries.Select(e => e.Target));
        Assert.All(entries, e => Assert.Equal((AuditActions.UserCreated, (int?)null, "setup"), (e.Action, e.ActorUserId, e.ActorName)));
    }

    [Fact]
    public async Task Setup_refuses_when_users_exist()
    {
        using var factory = new VenueAppFactory();
        await TestUsers.AddAsync(factory, "Nino", Role.Manager, "0123");

        var exitCode = await RunAsync(factory, new FakeConsole("Gio", "5678", "5678"));

        Assert.Equal(1, exitCode);
        await AssertNothingChangedAsync(factory, expectedUsers: 1);
    }

    [Theory]
    [InlineData("Nino", "0123", "0124")]
    [InlineData("Nino", "123", "123")]
    [InlineData("   ", "0123", "0123")]
    [InlineData("219VOLTS support", "0123", "0123")]
    public async Task Setup_rejects_mismatched_or_invalid_pin(string name, string pin, string pinAgain)
    {
        using var factory = new VenueAppFactory();

        var exitCode = await RunAsync(factory, new FakeConsole(name, pin, pinAgain));

        Assert.Equal(1, exitCode);
        await AssertNothingChangedAsync(factory, expectedUsers: 0);
    }

    private static Task<int> RunAsync(VenueAppFactory factory, FakeConsole console) =>
        new SetupCommand(factory.Services, console).RunAsync();

    private static async Task AssertNothingChangedAsync(VenueAppFactory factory, int expectedUsers)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        Assert.Equal(expectedUsers, await db.Users.CountAsync());
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }
}
