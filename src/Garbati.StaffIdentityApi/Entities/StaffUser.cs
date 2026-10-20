namespace Garbati.StaffIdentityApi.Entities;

// DESIGN.md Section 2.1. Id (the EF primary key) vs StaffId (the generated, public STF-XXXXXXXXXX id) is
// the same split every other GARBATI service uses for its own public identifiers (LedgerApi's
// AccountNumber, IdentityApi's CustomerId, OrchestratorApi's OperationId) - the internal Id is never
// exposed outside this service, and SupervisorStaffId below references the PUBLIC StaffId, not Id.
//
// IdempotencyKey/CreatedByClientId/RequestHash are infrastructure for POST /v1/staff's idempotency
// requirement (ARCHITECTURE.md Section 5), not part of DESIGN.md Section 2.1's field list - the same way
// OrchestratorApi's AccountOpening entity carries IdempotencyKey/InitiatedBy/RequestHash beyond what its
// own design doc's entity prose names. Null for the one row this service itself seeds (the initial
// Administrator, Migrations/*SeedInitialAdministrator*), which is never created through that endpoint.
public class StaffUser
{
    public long Id { get; set; }
    public string StaffId { get; set; } = null!;

    public string FullName { get; set; } = null!;
    public string Username { get; set; } = null!;

    // Case-insensitive uniqueness (task file decision, DESIGN.md 2.1 "Unique, case-insensitive"): a
    // normalized-lowercase shadow column with its own unique index (Data/StaffIdentityDbContext.cs),
    // rather than a citext column - no sibling service's database already depends on the citext
    // extension, and a plain unique index needs no extension to be installed on whatever Postgres
    // instance this gets deployed to. Written by Services/Implementations/StaffService.cs whenever
    // Username is set; never read directly by callers.
    public string NormalizedUsername { get; set; } = null!;

    // ASP.NET Core Identity's PasswordHasher<StaffUser> output (PBKDF2-HMAC-SHA256, DESIGN.md Section 4).
    // Never returned by any response contract - see Contracts/Responses/StaffResponse.cs.
    public string PasswordHash { get; set; } = null!;

    public Role Role { get; set; }
    public StaffStatus Status { get; set; }

    // True whenever an administrator sets/resets someone else's password (creation or admin-driven
    // reset); cleared only by the staff member's own self-service change (DESIGN.md 2.1).
    public bool MustChangePassword { get; set; }

    // Nullable, self-referencing to StaffId (not Id - see the comment at the top of this file). Validated
    // at assignment time by Services/Implementations/StaffService.cs (DESIGN.md 2.4, decision #4): must
    // name an existing, Active, Role = Supervisor StaffUser, and never the same staff member.
    public string? SupervisorStaffId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // Nullable only for the seeded Administrator's own row (DESIGN.md 2.5's same rule for
    // StaffAuditLog.ActingStaffId) - every other row is created by an already-authenticated administrator
    // acting through POST /v1/staff.
    public string? CreatedByStaffId { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    // POST /v1/staff idempotency (see the class comment above). Null for the seeded Administrator.
    public string? IdempotencyKey { get; set; }
    public string? CreatedByClientId { get; set; }
    public string? RequestHash { get; set; }
}
