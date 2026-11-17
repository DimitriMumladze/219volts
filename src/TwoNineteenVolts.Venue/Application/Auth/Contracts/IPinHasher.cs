namespace TwoNineteenVolts.Venue.Application.Auth.Contracts;

public interface IPinHasher
{
    string Hash(string secret);

    bool IsMatch(string hash, string secret);
}
