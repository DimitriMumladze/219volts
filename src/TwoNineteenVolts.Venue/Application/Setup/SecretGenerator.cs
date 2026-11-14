using System.Security.Cryptography;

namespace TwoNineteenVolts.Venue.Application.Setup;

public static class SecretGenerator
{
    private const int SupportPasswordLength = 24;

    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    public static string NewSupportPassword() => RandomNumberGenerator.GetString(Alphabet, SupportPasswordLength);
}
