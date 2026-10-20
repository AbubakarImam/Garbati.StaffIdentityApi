namespace Garbati.StaffIdentityApi.Contracts.Requests;

// DESIGN.md Section 3, endpoint 7. SupervisorStaffId is nullable (null unassigns).
public record ChangeSupervisorRequest(string ActingStaffId, string? SupervisorStaffId);
