using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Application.Auth;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Auth;
using TwoNineteenVolts.Venue.Infrastructure.Auth;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Tests.Auth;

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

        Assert.Equal(1, exitCode);
        Assert.True(await VerifyAsync(factory, support, OldPassword));
        Assert.Equal(0, await AuditCountAsync(factory));
    }

    [Fact]
    public async Task Reset_support_replaces_secret_and_audits()
    {
        using var factory = new VenueAppFactory();
        var support = await TestUsers.AddAsync(factory, "219volts Support", Role.Support, OldPassword);
        var console = new FakeConsole();

        var exitCode = await RunAsync(factory, console, isAdministrator: true);

        Assert.Equal(0, exitCode);
        Assert.False(await VerifyAsync(factory, support, OldPassword));
        var newPassword = Assert.Single(console.Output, line => line.Length == 24);
        Assert.True(await VerifyAsync(factory, support, newPassword));
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

        Assert.Equal(1, exitCode);
        Assert.Equal(0, await AuditCountAsync(factory));
    }

    private static async Task<int> RunAsync(VenueAppFactory factory, FakeConsole console, bool isAdministrator)
    {
        using var scope = factory.Services.CreateScope();
        var command = ActivatorUtilities.CreateInstance<ResetSupportCommand>(
            scope.ServiceProvider,
            console,
            (Func<bool>)(() => isAdministrator));
        return await command.RunAsync();
    }

    private static async Task<bool> VerifyAsync(VenueAppFactory factory, int userId, string password)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPinHasher>();
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        return hasher.Verify(user.SecretHash, password);
    }

    private static async Task<int> AuditCountAsync(VenueAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.AuditEntries.CountAsync();
    }
}
