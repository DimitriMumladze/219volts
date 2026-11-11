namespace TwoNineteenVolts.Venue.Application.Auth;

public record SessionInfo(string Key, int UserId, string UserName, DateTimeOffset LastActivity);
