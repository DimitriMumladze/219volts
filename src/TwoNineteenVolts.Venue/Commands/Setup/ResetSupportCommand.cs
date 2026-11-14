using Microsoft.Extensions.Localization;
using TwoNineteenVolts.Venue.Application.Setup;
using TwoNineteenVolts.Venue.Infrastructure.Data;
using TwoNineteenVolts.Venue.Resources;

namespace TwoNineteenVolts.Venue.Commands.Setup;

public class ResetSupportCommand(IServiceProvider services, IConsole console, Func<bool> isAdministrator)
{
    private readonly IStringLocalizer<Messages> _texts = services.GetRequiredService<IStringLocalizer<Messages>>();

    public async Task<int> RunAsync()
    {
        if (!isAdministrator())
        {
            return Fail(SetupMessages.NotAdministrator);
        }

        await services.MigrateDatabaseAsync();
        using var scope = services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ResetSupportService>().ResetAsync();
        if (result.IsFailure)
        {
            return Fail(result.Error);
        }

        console.WriteLine(_texts[SetupMessages.NewSupportPasswordIntro]);
        console.WriteLine(result.Value);
        console.WriteLine(_texts[SetupMessages.RestartService]);
        return 0;
    }


    private int Fail(string key)
    {
        console.WriteLine(_texts[SetupMessages.NothingChanged, _texts[key]]);
        return 1;
    }
}
