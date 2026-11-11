namespace TwoNineteenVolts.Venue.Domain.Audit;

public static class AuditActions
{
    public const string Login = "auth.login";
    public const string LoginFailed = "auth.login_failed";
    public const string Logout = "auth.logout";
    public const string SessionExpired = "auth.session_expired";

    public const string UserCreated = "user.created";
    public const string UserRenamed = "user.renamed";
    public const string UserRoleChanged = "user.role_changed";
    public const string UserPinSet = "user.pin_set";
    public const string UserRemoteAccessChanged = "user.remote_access_changed";
    public const string UserDeactivated = "user.deactivated";
    public const string UserReactivated = "user.reactivated";
    public const string SupportPasswordReset = "user.support_password_reset";
}
