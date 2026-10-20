using Garbati.StaffIdentityApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace Garbati.StaffIdentityApi.Data;

public class StaffIdentityDbContext(DbContextOptions<StaffIdentityDbContext> options) : DbContext(options)
{
    public DbSet<StaffUser> StaffUsers => Set<StaffUser>();
    public DbSet<StaffAuditLog> StaffAuditLogs => Set<StaffAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StaffUser>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => s.StaffId).IsUnique();

            // Case-insensitive username uniqueness enforced by a real database-level constraint, not just
            // an application check (task file decision) - a unique index on the normalized shadow column.
            entity.HasIndex(s => s.NormalizedUsername).IsUnique();

            // SupervisorStaffId is a self-reference to StaffId (the public id), not to the internal Id -
            // see Entities/StaffUser.cs. Restrict (not Cascade): a supervisor's own row is never deleted
            // by this service (no delete endpoint exists at all), so this only matters if one ever is.
            entity.HasOne<StaffUser>()
                .WithMany()
                .HasPrincipalKey(s => s.StaffId)
                .HasForeignKey(s => s.SupervisorStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(s => s.Role).HasConversion<string>();
            entity.Property(s => s.Status).HasConversion<string>();

            // Same scheme as every other idempotent create-something-new endpoint on this platform
            // (ARCHITECTURE.md Section 5: "unique per calling client"; OrchestratorApi decision #2,
            // LedgerApi #42/#43, IdentityApi #29). Nulls are not distinct so the seeded Administrator row
            // (both columns null) could never collide with a second null-key row, though no such second
            // row can ever exist in practice (every other row comes through POST /v1/staff, which
            // requires a key).
            entity.HasIndex(s => new { s.CreatedByClientId, s.IdempotencyKey }).IsUnique().AreNullsDistinct(false);

            entity.ToTable(t => t.HasCheckConstraint("ck_staff_users_role",
                "\"role\" IN ('Teller', 'CustomerServiceOfficer', 'Supervisor', 'ComplianceOfficer', 'RiskOfficer', 'Auditor', 'Administrator')"));
            entity.ToTable(t => t.HasCheckConstraint("ck_staff_users_status",
                "\"status\" IN ('Active', 'Suspended', 'Terminated')"));
        });

        modelBuilder.Entity<StaffAuditLog>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.TargetStaffId);
        });
    }
}
