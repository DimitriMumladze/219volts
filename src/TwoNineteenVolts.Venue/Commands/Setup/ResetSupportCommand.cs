using Microsoft.Extensions.Localization;
using TwoNineteenVolts.Venue.Application.Setup;
using TwoNineteenVolts.Venue.Infrastructure.Data;
using TwoNineteenVolts.Venue.Resources;

namespace TwoNineteenVolts.Venue.Commands.Setup;

// "TwoNineteenVolts.Venue.exe reset-support": for a lost Support password, run on the venue PC
// from an administrator command prompt. Not reachable over the network.
public class ResetSupportCommand(IServiceProvider services, IConsole console, Func<bool> isAdministrator)
{
    private readonly IStringLocalizer<Messages> _texts = services.GetRequiredService<IStringLocalizer<Messages>>();

    public async Task<int> RunAsync()
    {
        // Before the database is opened: a non-administrator may not be able to write the data
        // folder at all, and must get this message rather than a file-access error.
        if (!isAdministrator())
        {
            return Fail(SetupMessages.NotAdministrator);
        }

        await services.MigrateDatabaseAsync();
        using var scope = services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ResetSupportService>().ResetAsync();
        if (result.IsFailure)
        {
            return Fail(result.Error!);
        }

        console.WriteLine(_texts[SetupMessages.NewSupportPasswordIntro]);
        console.WriteLine(result.Value!);
        // Sessions live in the memory of the running service, which this separate process cannot reach.
        console.WriteLine(_texts[SetupMessages.RestartService]);
        return 0;
    }

    private int Fail(string key)
    {
        console.WriteLine(_texts[SetupMessages.NothingChanged, _texts[key]]);
        return 1;
    }
}
