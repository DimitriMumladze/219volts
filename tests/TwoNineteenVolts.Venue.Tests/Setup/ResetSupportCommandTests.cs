using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Application.Auth;
using TwoNineteenVolts.Venue.Commands.Setup;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Tests.Setup;

public class ResetSupportCommandTests
{
    private const string OldPassword = "old-support-password-123";

    [Fact]
    public async Task Reset_support_refuses_without_admin()
    {
        using var factory = new VenueAppFactory();
        var support = await TestUsers.AddAsync(factory, "219volts Support", Role.Support, OldPassword);
        var console = new FakeConsole();

        var exitCode = await RunAsync(factory, console, isAdministrator: false);

        var oldPasswordWorks = await VerifyAsync(factory, support, OldPassword);
        var auditCount = await AuditCountAsync(factory);
        Assert.Equal(1, exitCode);
        Assert.True(oldPasswordWorks);
        Assert.Equal(0, auditCount);
    }

    [Fact]
    public async Task Reset_support_without_admin_does_not_touch_the_database()
    {
        using var nothing = new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider();
        var console = new FakeConsole();

        var exitCode = await new ResetSupportCommand(nothing, console, () => false).RunAsync();

        Assert.Equal(1, exitCode);
        Assert.Contains(console.Output, line => line.Contains("administrator"));
    }

    [Fact]
    public async Task Reset_support_replaces_secret_and_audits()
    {
        using var factory = new VenueAppFactory();
        var support = await TestUsers.AddAsync(factory, "219volts Support", Role.Support, OldPassword);
        var console = new FakeConsole();

        var exitCode = await RunAsync(factory, console, isAdministrator: true);

        Assert.Equal(0, exitCode);
        var oldPasswordWorks = await VerifyAsync(factory, support, OldPassword);
        Assert.False(oldPasswordWorks);
        var newPassword = Assert.Single(console.Output, line => line.Length == 24);
        var newPasswordWorks = await VerifyAsync(factory, support, newPassword);
        Assert.True(newPasswordWorks);
        Assert.Contains(console.Output, line => line.Contains("restart the 219volts service", StringComparison.OrdinalIgnoreCase));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        var entry = await db.AuditEntries.SingleAsync();
        Assert.Equal((AuditActions.SupportPasswordReset, $"user:{support}", "reset-support"), (entry.Action, entry.Target, entry.ActorName));
    }

    [Fact]
    public async Task Reset_support_without_support_user_fails()
    {
        using var factory = new VenueAppFactory();

        var exitCode = await RunAsync(factory, new FakeConsole(), isAdministrator: true);

        var auditCount = await AuditCountAsync(factory);
        Assert.Equal(1, exitCode);
        Assert.Equal(0, auditCount);
    }


    private static Task<int> RunAsync(VenueAppFactory factory, FakeConsole console, bool isAdministrator) =>
        new ResetSupportCommand(factory.Services, console, () => isAdministrator).RunAsync();

    private static async Task<bool> VerifyAsync(VenueAppFactory factory, int userId, string password)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPinHasher>();
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        return hasher.IsMatch(user.SecretHash, password);
    }

    private static async Task<int> AuditCountAsync(VenueAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.AuditEntries.CountAsync();
    }
}
