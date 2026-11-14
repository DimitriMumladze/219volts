namespace TwoNineteenVolts.Venue.Application.Auth;

public static class PinFormat
{
    private const int Length = 4;

    public static bool IsValid(string pin) => pin.Length == Length && pin.All(char.IsAsciiDigit);
}
