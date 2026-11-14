using Microsoft.Extensions.Localization;
using TwoNineteenVolts.Venue.Application.Setup;
using TwoNineteenVolts.Venue.Infrastructure.Data;
using TwoNineteenVolts.Venue.Resources;

namespace TwoNineteenVolts.Venue.Commands.Setup;

public class SetupCommand(IServiceProvider services, IConsole console)
{
    private readonly IStringLocalizer<Messages> _texts = services.GetRequiredService<IStringLocalizer<Messages>>();

    public async Task<int> RunAsync()
    {
        await services.MigrateDatabaseAsync();
        using var scope = services.CreateScope();
        var setup = scope.ServiceProvider.GetRequiredService<SetupService>();

        if (await setup.IsDoneAsync())
        {
            return Fail(SetupError.AlreadyDone);
        }

        console.WriteLine(_texts[SetupMessages.OwnerNamePrompt]);
        var name = console.ReadLine();
        console.WriteLine(_texts[SetupMessages.OwnerPinPrompt]);
        var pin = console.ReadSecret();
        console.WriteLine(_texts[SetupMessages.PinAgainPrompt]);
        if (console.ReadSecret() != pin)
        {
            return Fail(SetupMessages.PinsDoNotMatch);
        }

        var result = await setup.SetUpAsync(name, pin);
        if (result.IsFailure)
        {
            return Fail(result.Error);
        }

        var ownerName = name.Trim();
        console.WriteLine(_texts[SetupMessages.Created, ownerName]);
        console.WriteLine(_texts[SetupMessages.SupportPasswordIntro]);
        console.WriteLine(result.Value);
        return 0;
    }


    private int Fail(string key)
    {
        console.WriteLine(_texts[SetupMessages.NothingChanged, _texts[key]]);
        return 1;
    }
}
