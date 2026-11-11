namespace TwoNineteenVolts.Venue.Application.Auth;

public interface IPinHasher
{
    string Hash(string secret);

    bool Verify(string hash, string secret);
}
