using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using TwoNineteenVolts.Venue.Application.Auth;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Application.Groups;
using TwoNineteenVolts.Venue.Application.Sessions;
using TwoNineteenVolts.Venue.Application.Setup;
using TwoNineteenVolts.Venue.Application.Spots;
using TwoNineteenVolts.Venue.Application.Users;
using TwoNineteenVolts.Venue.Commands.Setup;
using TwoNineteenVolts.Venue.Resources;

namespace TwoNineteenVolts.Venue.Tests.Resources;

public class MessagesTests
{
    private static readonly Type[] KeyClasses =
    [
        typeof(UserError),
        typeof(SetupError),
        typeof(AuthError),
        typeof(SetupMessages),
        typeof(DeviceError),
        typeof(SpotError),
        typeof(GroupError),
        typeof(SessionError)
    ];

    [Fact]
    public void Every_key_has_an_English_text()
    {
        using var factory = new VenueAppFactory();
        var texts = factory.Services.GetRequiredService<IStringLocalizer<Messages>>();

        var missing = KeyClasses
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .Where(key => texts[key].ResourceNotFound)
            .ToList();

        Assert.Empty(missing);
    }
}
