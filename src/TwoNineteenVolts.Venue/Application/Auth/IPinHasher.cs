namespace TwoNineteenVolts.Venue.Application.Auth;

public interface IPinHasher
{
    string Hash(string secret);

    bool IsMatch(string hash, string secret);
}
