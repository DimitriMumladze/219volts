using TwoNineteenVolts.Venue.Application.Setup;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Commands.Setup;

// "TwoNineteenVolts.Venue.exe reset-support": for a lost Support password, run on the venue PC
// from an administrator command prompt. Not reachable over the network.
public class ResetSupportCommand(IServiceProvider services, IConsole console, Func<bool> isAdministrator)
{
    public async Task<int> RunAsync()
    {
        // Before the database is opened: a non-administrator may not be able to write the data
        // folder at all, and must get this message rather than a file-access error.
        if (!isAdministrator())
        {
            return Fail("Run this from an administrator command prompt");
        }

        await services.MigrateDatabaseAsync();
        using var scope = services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ResetSupportService>().ResetAsync();
        if (result.IsFailure)
        {
            return Fail(result.Error!);
        }

        console.WriteLine("New support password. Save it in the password manager now; it is shown only this once:");
        console.WriteLine(result.Value!);
        // Sessions live in the memory of the running service, which this separate process cannot reach.
        console.WriteLine("Then restart the 219volts service to sign out active Support sessions.");
        return 0;
    }

    private int Fail(string message)
    {
        console.WriteLine($"{message}. Nothing was changed.");
        return 1;
    }
}
