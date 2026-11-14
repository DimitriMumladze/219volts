using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Endpoints.Users;

public record UserResponse(int Id, string DisplayName, Role Role, bool IsActive, DateTime CreatedAt);
