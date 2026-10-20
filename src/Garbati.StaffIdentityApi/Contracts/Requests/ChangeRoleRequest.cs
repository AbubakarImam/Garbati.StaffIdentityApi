namespace Garbati.StaffIdentityApi.Contracts.Requests;

// DESIGN.md Section 3, endpoint 5. Deliberately does not touch SupervisorStaffId - see
// Services/Implementations/StaffService.cs.ChangeRoleAsync.
public record ChangeRoleRequest(string ActingStaffId, string Role);
