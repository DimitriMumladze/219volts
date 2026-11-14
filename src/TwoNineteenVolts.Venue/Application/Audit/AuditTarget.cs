namespace TwoNineteenVolts.Venue.Application.Audit;

public static class AuditTarget
{
    public static string User(int? userId) => $"user:{userId}";

    public static string Device(int deviceId) => $"device:{deviceId}";
}
