using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Application.Auth;
using TwoNineteenVolts.Venue.Application.Users;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Infrastructure.Auth;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Tests.Users;

public class UserServiceTests
{
    [Theory]
    [InlineData(" nino ")]
    [InlineData("NINO")]
    [InlineData("Nino")]
    public async Task Create_rejects_name_variants_of_existing(string name)
    {
        using var factory = new VenueAppFactory();
        await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");

        var result = await UseServiceAsync(factory, users => users.CreateAsync(name, "5678"));

        Assert.Equal(UserError.DuplicateName, result.Error);
    }

    [Fact]
    public async Task Create_stores_trimmed_name_and_hashed_pin()
    {
        using var factory = new VenueAppFactory();

        var result = await UseServiceAsync(factory, users => users.CreateAsync("  Gio ", "0123"));

        var gio = await FindAsync(factory, result.Value!.Id);
        Assert.Equal("Gio", gio.DisplayName);
        Assert.Equal(Role.Staff, gio.Role);
        Assert.True(gio.IsActive);
        Assert.True(factory.Services.GetRequiredService<IPinHasher>().Verify(gio.SecretHash, "0123"));
    }

    [Fact]
    public async Task Create_rejects_empty_name_and_bad_pin()
    {
        using var factory = new VenueAppFactory();

        Assert.Equal(UserError.InvalidName, (await UseServiceAsync(factory, users => users.CreateAsync("   ", "0123"))).Error);
        Assert.Equal(UserError.InvalidPin, (await UseServiceAsync(factory, users => users.CreateAsync("Gio", "123"))).Error);
    }

    [Fact]
    public async Task Rename_to_variant_of_other_user_rejected()
    {
        using var factory = new VenueAppFactory();
        await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");
        var gio = await TestUsers.AddAsync(factory, "Gio", Role.Staff, "5678");

        var result = await UseServiceAsync(factory, users => users.RenameAsync(gio, " nINo "));

        Assert.Equal(UserError.DuplicateName, result.Error);
        Assert.Equal("Gio", (await FindAsync(factory, gio)).DisplayName);
    }

    [Fact]
    public async Task Rename_to_own_name_in_other_case_is_allowed()
    {
        using var factory = new VenueAppFactory();
        var gio = await TestUsers.AddAsync(factory, "gio", Role.Staff, "5678");

        var result = await UseServiceAsync(factory, users => users.RenameAsync(gio, "Gio"));

        Assert.False(result.IsFailure);
        Assert.Equal("Gio", (await FindAsync(factory, gio)).DisplayName);
    }

    [Fact]
    public async Task Rename_frees_old_name()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Staff, "1234");

        await UseServiceAsync(factory, users => users.RenameAsync(nino, "Nina"));
        var result = await UseServiceAsync(factory, users => users.CreateAsync("Nino", "5678"));

        Assert.False(result.IsFailure);
    }

    [Fact]
    public async Task Support_user_is_not_found_for_every_operation()
    {
        using var factory = new VenueAppFactory();
        var support = await TestUsers.AddAsync(factory, "219volts Support", Role.Support, "support-password");

        Assert.Equal(UserError.NotFound, (await UseServiceAsync(factory, users => users.RenameAsync(support, "Eve"))).Error);
        Assert.Equal(UserError.NotFound, (await UseServiceAsync(factory, users => users.SetPinAsync(support, "1234"))).Error);
        Assert.Equal(UserError.NotFound, (await UseServiceAsync(factory, users => users.DeactivateAsync(support))).Error);
        Assert.Equal(UserError.NotFound, (await UseServiceAsync(factory, users => users.ReactivateAsync(support))).Error);
        Assert.Equal(UserError.NotFound, (await UseServiceAsync(factory, users => users.RenameAsync(999, "Eve"))).Error);
    }

    [Fact]
    public async Task Manager_cannot_be_deactivated()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");

        var result = await UseServiceAsync(factory, users => users.DeactivateAsync(nino));

        Assert.Equal(UserError.ManagerCannotBeDeactivated, result.Error);
        Assert.True((await FindAsync(factory, nino)).IsActive);
    }

    [Fact]
    public async Task Deactivate_ends_sessions()
    {
        using var factory = new VenueAppFactory();
        var gio = await TestUsers.AddAsync(factory, "Gio", Role.Staff, "5678");
        var store = factory.Services.GetRequiredService<InMemoryTicketStore>();
        var session = await store.StoreAsync(TicketFor(gio, "Gio"));

        await UseServiceAsync(factory, users => users.DeactivateAsync(gio));

        Assert.Null(await store.RetrieveAsync(session));
    }

    [Fact]
    public async Task Rename_ends_sessions_so_the_new_name_is_used()
    {
        using var factory = new VenueAppFactory();
        var gio = await TestUsers.AddAsync(factory, "Gio", Role.Staff, "5678");
        var store = factory.Services.GetRequiredService<InMemoryTicketStore>();
        var session = await store.StoreAsync(TicketFor(gio, "Gio"));

        await UseServiceAsync(factory, users => users.RenameAsync(gio, "Giorgi"));

        Assert.Null(await store.RetrieveAsync(session));
    }

    [Fact]
    public async Task Pin_change_ends_sessions_so_a_leaked_pin_stops_working()
    {
        using var factory = new VenueAppFactory();
        var gio = await TestUsers.AddAsync(factory, "Gio", Role.Staff, "5678");
        var store = factory.Services.GetRequiredService<InMemoryTicketStore>();
        var session = await store.StoreAsync(TicketFor(gio, "Gio"));

        await UseServiceAsync(factory, users => users.SetPinAsync(gio, "0000"));

        Assert.Null(await store.RetrieveAsync(session));
    }

    [Fact]
    public async Task Failed_rename_keeps_sessions()
    {
        using var factory = new VenueAppFactory();
        await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");
        var gio = await TestUsers.AddAsync(factory, "Gio", Role.Staff, "5678");
        var store = factory.Services.GetRequiredService<InMemoryTicketStore>();
        var session = await store.StoreAsync(TicketFor(gio, "Gio"));

        await UseServiceAsync(factory, users => users.RenameAsync(gio, "nino"));

        Assert.NotNull(await store.RetrieveAsync(session));
    }

    [Fact]
    public async Task Each_change_records_audit_entry()
    {
        using var factory = new VenueAppFactory();
        await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");

        var gio = (await UseServiceAsync(factory, users => users.CreateAsync("Gio", "5678"))).Value!.Id;
        await UseServiceAsync(factory, users => users.RenameAsync(gio, "Giorgi"));
        await UseServiceAsync(factory, users => users.SetPinAsync(gio, "0000"));
        await UseServiceAsync(factory, users => users.DeactivateAsync(gio));
        await UseServiceAsync(factory, users => users.ReactivateAsync(gio));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        var entries = await db.AuditEntries.OrderBy(e => e.Id).ToListAsync();

        Assert.Equal(
            [
                (AuditActions.UserCreated, (string?)null),
                (AuditActions.UserRenamed, "Gio"),
                (AuditActions.UserPinSet, null),
                (AuditActions.UserDeactivated, null),
                (AuditActions.UserReactivated, null)
            ],
            entries.Select(e => (e.Action, e.Details)));
        Assert.All(entries, e => Assert.Equal($"user:{gio}", e.Target));
    }

    [Fact]
    public async Task Failed_change_records_nothing()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");

        await UseServiceAsync(factory, users => users.DeactivateAsync(nino));
        await UseServiceAsync(factory, users => users.CreateAsync("nino", "1234"));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    private static async Task<Result<User>> UseServiceAsync(VenueAppFactory factory, Func<UserService, Task<Result<User>>> action)
    {
        using var scope = factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<UserService>());
    }

    private static async Task<User> FindAsync(VenueAppFactory factory, int id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.Users.SingleAsync(u => u.Id == id);
    }

    private static AuthenticationTicket TicketFor(int userId, string name)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(AuthClaims.UserId, userId.ToString()),
                new Claim(AuthClaims.Name, name)
            ],
            "test");

        return new AuthenticationTicket(new ClaimsPrincipal(identity), "test");
    }
}
