using Microsoft.AspNetCore.Identity;
using TwoNineteenVolts.Venue.Application.Auth;
using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Infrastructure.Auth;

public class PinHasher : IPinHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string secret) => _hasher.HashPassword(null!, secret);

    public bool Verify(string hash, string secret) =>
        _hasher.VerifyHashedPassword(null!, hash, secret) != PasswordVerificationResult.Failed;
}
