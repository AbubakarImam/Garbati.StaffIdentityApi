namespace Garbati.StaffIdentityApi.Contracts.Responses;

// DESIGN.md Section 3, endpoint 1: "200 {staffId, fullName, role} on success".
public record AuthenticateResponse(string StaffId, string FullName, string Role);
