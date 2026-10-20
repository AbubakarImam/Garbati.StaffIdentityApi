namespace Garbati.StaffIdentityApi.Contracts.Requests;

// DESIGN.md Section 3, endpoint 8 - one request shape serving both branches of one route
// (Controllers/StaffController.cs.ChangePassword decides which fields matter, based on the caller's own
// scope claim, not on anything in this body):
//   - staffidentity.admin (admin-driven reset): only ActingStaffId is read. CurrentPassword/NewPassword
//     are ignored even if present - a new one-time temporary credential is always generated server-side
//     (the same convention POST /v1/staff already uses), never accepted as plaintext from an admin
//     caller. See the PR description for why this is a judgment call, not something DESIGN.md spells out.
//   - staffidentity.authenticate (self-service change): ActingStaffId, CurrentPassword and NewPassword
//     are all required; CurrentPassword is verified, NewPassword becomes the new PasswordHash.
public record ChangeStaffPasswordRequest(string ActingStaffId, string? CurrentPassword, string? NewPassword);
