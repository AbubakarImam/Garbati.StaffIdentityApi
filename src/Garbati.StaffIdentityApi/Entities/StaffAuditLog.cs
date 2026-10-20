namespace Garbati.StaffIdentityApi.Entities;

// DESIGN.md Section 2.5 - this service's OWN audit shape, deliberately narrower than LedgerApi's/
// OrchestratorApi's AuditLog (task file: "use that table, do not reuse another service's AuditLogs shape
// verbatim if the field names differ"). Exactly the five fields the design doc names; no correlation id,
// endpoint, IP or user-agent columns here.
public class StaffAuditLog
{
    public long Id { get; set; }

    // One of Auditing/StaffAuditActions.cs's constants.
    public string Action { get; set; } = null!;

    // Whose account this is about (the StaffId, not the internal Id).
    public string TargetStaffId { get; set; } = null!;

    // Who did it. Nullable only for AuthenticationFailed (the attempted username lives in Details, never
    // the password) and for the seeded Administrator's own StaffCreated row.
    public string? ActingStaffId { get; set; }

    // Free text - e.g. old role -> new role, old status -> new status, or the attempted username on a
    // failed authentication. Never a password or password hash.
    public string? Details { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
