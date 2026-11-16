namespace TwoNineteenVolts.Venue.Application.Audit;

public static class AuditTarget
{
    public static string User(int? userId) => $"user:{userId}";

    public static string Device(int deviceId) => $"device:{deviceId}";

    public static string Spot(int spotId) => $"spot:{spotId}";

    public static string Group(int groupId) => $"group:{groupId}";
}
