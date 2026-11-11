using TwoNineteenVolts.Venue.Domain.Auth;

namespace TwoNineteenVolts.Venue.Endpoints.Users;

public record UserResponse(int Id, string DisplayName, Role Role, bool IsActive, bool RemoteAccessAllowed, DateTime CreatedAt);
