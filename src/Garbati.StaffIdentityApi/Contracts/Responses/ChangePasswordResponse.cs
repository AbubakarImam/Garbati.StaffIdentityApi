namespace Garbati.StaffIdentityApi.Contracts.Responses;

// DESIGN.md Section 3, endpoint 8. TemporaryPassword is set only on the admin-driven reset branch (a
// freshly generated one-time credential, same convention as CreateStaffResponse) - null on the
// self-service branch, which has no secret to hand back (the caller already knows their own new password).
public record ChangePasswordResponse(string StaffId, string? TemporaryPassword);
