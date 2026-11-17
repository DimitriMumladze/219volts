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
        var hasher = factory.Services.GetRequiredService<IPinHasher>();
        var isPinMatch = hasher.IsMatch(gio.SecretHash, "0123");
        Assert.True(isPinMatch);
    }

    [Fact]
    public async Task Create_rejects_empty_name_and_bad_pin()
    {
        using var factory = new VenueAppFactory();

        var emptyName = await UseServiceAsync(factory, users => users.CreateAsync("   ", "0123"));
        var shortPin = await UseServiceAsync(factory, users => users.CreateAsync("Gio", "123"));

        Assert.Equal(UserError.InvalidName, emptyName.Error);
        Assert.Equal(UserError.InvalidPin, shortPin.Error);
    }

    [Fact]
    public async Task Rename_to_variant_of_other_user_rejected()
    {
        using var factory = new VenueAppFactory();
        await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");
        var gio = await TestUsers.AddAsync(factory, "Gio", Role.Staff, "5678");

        var result = await UseServiceAsync(factory, users => users.RenameAsync(gio, " nINo "));

        Assert.Equal(UserError.DuplicateName, result.Error);
        var stored = await FindAsync(factory, gio);
        Assert.Equal("Gio", stored.DisplayName);
    }

    [Fact]
    public async Task Rename_to_own_name_in_other_case_is_allowed()
    {
        using var factory = new VenueAppFactory();
        var gio = await TestUsers.AddAsync(factory, "gio", Role.Staff, "5678");

        var result = await UseServiceAsync(factory, users => users.RenameAsync(gio, "Gio"));

        Assert.False(result.IsFailure);
        var stored = await FindAsync(factory, gio);
        Assert.Equal("Gio", stored.DisplayName);
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

        var rename = await UseServiceAsync(factory, users => users.RenameAsync(support, "Eve"));
        var setPin = await UseServiceAsync(factory, users => users.SetPinAsync(support, "1234"));
        var deactivate = await UseServiceAsync(factory, users => users.DeactivateAsync(support));
        var reactivate = await UseServiceAsync(factory, users => users.ReactivateAsync(support));
        var unknown = await UseServiceAsync(factory, users => users.RenameAsync(999, "Eve"));

        Assert.Equal(UserError.NotFound, rename.Error);
        Assert.Equal(UserError.NotFound, setPin.Error);
        Assert.Equal(UserError.NotFound, deactivate.Error);
        Assert.Equal(UserError.NotFound, reactivate.Error);
        Assert.Equal(UserError.NotFound, unknown.Error);
    }

    [Fact]
    public async Task Manager_cannot_be_deactivated()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");

        var result = await UseServiceAsync(factory, users => users.DeactivateAsync(nino));

        Assert.Equal(UserError.ManagerCannotBeDeactivated, result.Error);
        var stored = await FindAsync(factory, nino);
        Assert.True(stored.IsActive);
    }

    [Fact]
    public async Task Deactivate_ends_sessions()
    {
        using var factory = new VenueAppFactory();
        var gio = await TestUsers.AddAsync(factory, "Gio", Role.Staff, "5678");
        var store = factory.Services.GetRequiredService<InMemoryTicketStore>();
        var ticket = TicketFor(gio, "Gio");
        var session = await store.StoreAsync(ticket);

        await UseServiceAsync(factory, users => users.DeactivateAsync(gio));

        var stored = await store.RetrieveAsync(session);

        Assert.Null(stored);
    }

    [Fact]
    public async Task Rename_ends_sessions_so_the_new_name_is_used()
    {
        using var factory = new VenueAppFactory();
        var gio = await TestUsers.AddAsync(factory, "Gio", Role.Staff, "5678");
        var store = factory.Services.GetRequiredService<InMemoryTicketStore>();
        var ticket = TicketFor(gio, "Gio");
        var session = await store.StoreAsync(ticket);

        await UseServiceAsync(factory, users => users.RenameAsync(gio, "Giorgi"));

        var stored = await store.RetrieveAsync(session);

        Assert.Null(stored);
    }

    [Fact]
    public async Task Pin_change_ends_sessions_so_a_leaked_pin_stops_working()
    {
        using var factory = new VenueAppFactory();
        var gio = await TestUsers.AddAsync(factory, "Gio", Role.Staff, "5678");
        var store = factory.Services.GetRequiredService<InMemoryTicketStore>();
        var ticket = TicketFor(gio, "Gio");
        var session = await store.StoreAsync(ticket);

        await UseServiceAsync(factory, users => users.SetPinAsync(gio, "0000"));

        var stored = await store.RetrieveAsync(session);

        Assert.Null(stored);
    }

    [Fact]
    public async Task Failed_rename_keeps_sessions()
    {
        using var factory = new VenueAppFactory();
        await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");
        var gio = await TestUsers.AddAsync(factory, "Gio", Role.Staff, "5678");
        var store = factory.Services.GetRequiredService<InMemoryTicketStore>();
        var ticket = TicketFor(gio, "Gio");
        var session = await store.StoreAsync(ticket);

        await UseServiceAsync(factory, users => users.RenameAsync(gio, "nino"));

        var stored = await store.RetrieveAsync(session);

        Assert.NotNull(stored);
    }

    [Fact]
    public async Task Each_change_records_audit_entry()
    {
        using var factory = new VenueAppFactory();
        await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");

        var created = await UseServiceAsync(factory, users => users.CreateAsync("Gio", "5678"));
        var gio = created.Value!.Id;
        await UseServiceAsync(factory, users => users.RenameAsync(gio, "Giorgi"));
        await UseServiceAsync(factory, users => users.SetPinAsync(gio, "0000"));
        await UseServiceAsync(factory, users => users.DeactivateAsync(gio));
        await UseServiceAsync(factory, users => users.ReactivateAsync(gio));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        var entries = await db.AuditEntries.OrderBy(e => e.Id).ToListAsync();
        var actionsAndDetails = entries.Select(e => (e.Action, e.Details));

        Assert.Equal(
            [
                (AuditActions.UserCreated, (string?)null),
                (AuditActions.UserRenamed, "Gio"),
                (AuditActions.UserPinSet, null),
                (AuditActions.UserDeactivated, null),
                (AuditActions.UserReactivated, null)
            ],
            actionsAndDetails);
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
        var count = await db.AuditEntries.CountAsync();
        Assert.Equal(0, count);
    }

    [Theory]
    [InlineData("Нино", "НИНО")]
    [InlineData("ნინო", "ᲜᲘᲜᲝ")]
    public async Task Names_differing_only_in_case_in_any_language_are_duplicates(string existing, string candidate)
    {
        using var factory = new VenueAppFactory();
        await UseServiceAsync(factory, users => users.CreateAsync(existing, "1234"));

        var result = await UseServiceAsync(factory, users => users.CreateAsync(candidate, "5678"));

        Assert.Equal(UserError.DuplicateName, result.Error);
    }


    private static async Task<Result<User>> UseServiceAsync(VenueAppFactory factory, Func<UserService, Task<Result<User>>> action)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<UserService>();
        return await action(service);
    }

    private static async Task<User> FindAsync(VenueAppFactory factory, int id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.Users.SingleAsync(u => u.Id == id);
    }

    private static AuthenticationTicket TicketFor(int userId, string name)
    {
        var id = userId.ToString();
        var identity = new ClaimsIdentity(
            [
                new Claim(AuthClaims.UserId, id),
                new Claim(AuthClaims.Name, name)
            ],
            "test");

        var principal = new ClaimsPrincipal(identity);
        return new AuthenticationTicket(principal, "test");
    }
}
