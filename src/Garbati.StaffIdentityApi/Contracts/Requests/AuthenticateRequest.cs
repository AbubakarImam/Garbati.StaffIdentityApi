namespace Garbati.StaffIdentityApi.Contracts.Requests;

// DESIGN.md Section 3, endpoint 1.
public record AuthenticateRequest(string Username, string Password);
