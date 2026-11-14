namespace TwoNineteenVolts.Venue.Domain.Audit;

public static class AuditActions
{
    public const string Login = "auth.login";
    public const string LoginFailed = "auth.login_failed";
    public const string Logout = "auth.logout";

    public const string UserCreated = "user.created";
    public const string UserRenamed = "user.renamed";
    public const string UserPinSet = "user.pin_set";
    public const string UserDeactivated = "user.deactivated";
    public const string UserReactivated = "user.reactivated";
    public const string SupportPasswordReset = "user.support_password_reset";

    public const string DeviceRegistered = "device.registered";
    public const string DeviceUpdated = "device.updated";
    public const string DeviceDeleted = "device.deleted";

    public const string SpotCreated = "spot.created";
    public const string SpotUpdated = "spot.updated";
    public const string SpotCodeChanged = "spot.code_changed";
    public const string SpotArchived = "spot.archived";
    public const string SpotSwitchedOn = "spot.switched_on";
    public const string SpotSwitchedOff = "spot.switched_off";
    public const string SpotSwitchFailed = "spot.switch_failed";
}
