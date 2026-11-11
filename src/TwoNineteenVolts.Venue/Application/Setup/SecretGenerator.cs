using System.Security.Cryptography;

namespace TwoNineteenVolts.Venue.Application.Setup;

public static class SecretGenerator
{
    private const int SupportPasswordLength = 24;

    // Letters and digits without look-alikes (0/O, 1/l/I), so the password can be typed from
    // the password manager on a tablet without mistakes. 24 of 57 characters ≈ 140 bits.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    public static string NewSupportPassword() => RandomNumberGenerator.GetString(Alphabet, SupportPasswordLength);
}
