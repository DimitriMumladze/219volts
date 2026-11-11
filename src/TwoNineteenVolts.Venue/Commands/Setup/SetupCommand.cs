using TwoNineteenVolts.Venue.Application.Setup;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Commands.Setup;

// "TwoNineteenVolts.Venue.exe setup": run once by our team when installing a venue.
// Only the conversation with the installer lives here; the rules are in SetupService.
public class SetupCommand(IServiceProvider services, IConsole console)
{
    public async Task<int> RunAsync()
    {
        await services.MigrateDatabaseAsync();
        using var scope = services.CreateScope();
        var setup = scope.ServiceProvider.GetRequiredService<SetupService>();

        // Checked before any question, so nobody types a name and PIN only to be refused.
        if (await setup.IsDoneAsync())
        {
            return Fail(SetupError.AlreadyDone);
        }

        console.WriteLine("Display name of the owner:");
        var name = console.ReadLine();
        console.WriteLine("PIN for the owner (4 digits):");
        var pin = console.ReadSecret();
        console.WriteLine("Same PIN again:");
        if (console.ReadSecret() != pin)
        {
            return Fail("The PINs do not match");
        }

        var result = await setup.SetUpAsync(name, pin);
        if (result.IsFailure)
        {
            return Fail(result.Error!);
        }

        console.WriteLine($"Created manager \"{name.Trim()}\" and the support user.");
        console.WriteLine("Support password. Save it in the password manager now; it is shown only this once:");
        console.WriteLine(result.Value!);
        return 0;
    }

    private int Fail(string message)
    {
        console.WriteLine($"{message}. Nothing was changed.");
        return 1;
    }
}
