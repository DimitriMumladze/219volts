using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Endpoints.Users;

public record CreateUserRequest(string DisplayName, Role Role, string Pin);
