using TwoNineteenVolts.Venue.Domain.Auth;

namespace TwoNineteenVolts.Venue.Endpoints.Auth;

public record CurrentUserResponse(int Id, string DisplayName, Role Role);
