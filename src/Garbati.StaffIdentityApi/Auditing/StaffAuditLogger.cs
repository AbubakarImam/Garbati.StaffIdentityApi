using Garbati.StaffIdentityApi.Data;
using Garbati.StaffIdentityApi.Entities;

namespace Garbati.StaffIdentityApi.Auditing;

// Unlike OrchestratorApi's AuditLogger, this does not read HttpContext for ActorId - StaffAuditLog's
// ActingStaffId is a business actor (a StaffId, from the request body's actingStaffId field), never the
// calling API client id. See Entities/StaffAuditLog.cs.
public class StaffAuditLogger(StaffIdentityDbContext dbContext) : IStaffAuditLogger
{
    public StaffAuditLog Record(StaffAuditLog entry)
    {
        entry.CreatedAt = DateTimeOffset.UtcNow;
        dbContext.StaffAuditLogs.Add(entry);
        return entry;
    }
}
