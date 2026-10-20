namespace Garbati.StaffIdentityApi.Auditing;

// DESIGN.md Section 2.5: the fixed action vocabulary for StaffAuditLog.Action.
public static class StaffAuditActions
{
    public const string StaffCreated = "StaffCreated";
    public const string RoleChanged = "RoleChanged";
    public const string StatusChanged = "StatusChanged";
    public const string SupervisorChanged = "SupervisorChanged";
    public const string PasswordReset = "PasswordReset";
    public const string AuthenticationSucceeded = "AuthenticationSucceeded";
    public const string AuthenticationFailed = "AuthenticationFailed";
}
