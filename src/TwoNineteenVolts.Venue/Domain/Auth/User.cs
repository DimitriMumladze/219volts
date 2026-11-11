namespace TwoNineteenVolts.Venue.Domain.Auth;

public class User(string displayName, Role role, string secretHash, DateTime createdAt)
{
    public int Id { get; private set; }

    public string DisplayName { get; private set; } = CleanName(displayName);

    public Role Role { get; private set; } = role;

    public string SecretHash { get; private set; } = secretHash;

    public bool IsActive { get; private set; } = true;

    public bool RemoteAccessAllowed { get; private set; }

    public DateTime CreatedAt { get; private set; } = createdAt;

    public void Rename(string displayName) => DisplayName = CleanName(displayName);

    public void ChangeRole(Role role) => Role = role;

    public void SetSecret(string secretHash) => SecretHash = secretHash;

    public void SetRemoteAccess(bool allowed) => RemoteAccessAllowed = allowed;

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    // Callers validate the name first; an empty name reaching this point is a bug.
    private static string CleanName(string displayName)
    {
        var trimmed = displayName.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Display name must not be empty.", nameof(displayName));
        }

        return trimmed;
    }
}
