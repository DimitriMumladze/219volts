namespace TwoNineteenVolts.Venue.Application.Auth;

public static class PinFormat
{
    private const int Length = 4;

    // ASCII only: char.IsDigit would also accept digits from other scripts, such as "١٢٣٤".
    public static bool IsValid(string pin) => pin.Length == Length && pin.All(char.IsAsciiDigit);
}
