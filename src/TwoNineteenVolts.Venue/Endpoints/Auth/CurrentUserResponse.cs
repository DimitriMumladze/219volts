using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Endpoints.Auth;

public record CurrentUserResponse(int Id, string DisplayName, Role Role);
