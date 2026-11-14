namespace TwoNineteenVolts.Venue.Domain.Users;

public class User(string displayName, Role role, string secretHash, DateTime createdAt)
{
    public int Id { get; private set; }

    public string DisplayName { get; private set; } = displayName;

    public string DisplayNameKey { get; private set; } = displayName.ToUpperInvariant();

    public Role Role { get; private set; } = role;

    public string SecretHash { get; private set; } = secretHash;

    public bool IsActive { get; private set; } = true;

    public DateTime CreatedAt { get; private set; } = createdAt;

    public void Rename(string displayName)
    {
        DisplayName = displayName;
        DisplayNameKey = displayName.ToUpperInvariant();
    }

    public void SetSecret(string secretHash) => SecretHash = secretHash;

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
