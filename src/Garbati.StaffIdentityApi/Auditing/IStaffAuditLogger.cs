using Garbati.StaffIdentityApi.Entities;

namespace Garbati.StaffIdentityApi.Auditing;

public interface IStaffAuditLogger
{
    // Stages an audit row on the current DbContext; it is written by the caller's next SaveChanges, so it
    // commits (or rolls back) together with the change it describes (ARCHITECTURE.md Section 5: "written
    // in the same database transaction as the change it describes").
    StaffAuditLog Record(StaffAuditLog entry);
}
