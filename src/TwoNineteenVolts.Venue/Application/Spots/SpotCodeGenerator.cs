using System.Security.Cryptography;

namespace TwoNineteenVolts.Venue.Application.Spots;

public class SpotCodeGenerator : ISpotCodeGenerator
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
    private const int Length = 6;

    public string NewCode()
    {
        return RandomNumberGenerator.GetString(Alphabet, Length);
    }
}
