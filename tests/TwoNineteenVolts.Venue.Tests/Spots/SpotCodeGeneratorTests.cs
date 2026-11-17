using System.Text.RegularExpressions;
using TwoNineteenVolts.Venue.Application.Spots;

namespace TwoNineteenVolts.Venue.Tests.Spots;

public class SpotCodeGeneratorTests
{
    [Fact]
    public void Code_is_six_characters_of_a_to_z_and_0_to_9()
    {
        var generator = new SpotCodeGenerator();

        for (var attempt = 1; attempt <= 100; attempt++)
        {
            var code = generator.NewCode();
            var isCodeShapeRight = Regex.IsMatch(code, "^[a-z0-9]{6}$");
            Assert.True(isCodeShapeRight, code);
        }
    }

    [Fact]
    public void Codes_differ()
    {
        var generator = new SpotCodeGenerator();

        var codes = Enumerable.Range(1, 100)
            .Select(_ => generator.NewCode())
            .ToHashSet();

        Assert.True(codes.Count > 90);
    }
}
