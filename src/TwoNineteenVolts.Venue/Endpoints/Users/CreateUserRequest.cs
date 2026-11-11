using TwoNineteenVolts.Venue.Domain.Auth;

namespace TwoNineteenVolts.Venue.Endpoints.Users;

public record CreateUserRequest(string DisplayName, Role Role, string Pin);
