namespace Garbati.StaffIdentityApi.Contracts.Requests;

// DESIGN.md Section 3, endpoint 6.
public record ChangeStatusRequest(string ActingStaffId, string Status);
