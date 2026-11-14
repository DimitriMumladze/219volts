using TwoNineteenVolts.Venue.Application.Spots;

namespace TwoNineteenVolts.Venue.Tests.Spots;

public class FixedSpotCodeGenerator(string code) : ISpotCodeGenerator
{
    public string NewCode()
    {
        return code;
    }
}
