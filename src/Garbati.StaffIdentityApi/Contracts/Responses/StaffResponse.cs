namespace Garbati.StaffIdentityApi.Contracts.Responses;

// DESIGN.md Section 3, endpoints 3/4: "Never returns PasswordHash. Includes supervisorStaffId." Also the
// shape returned (without TemporaryPassword) by every PATCH endpoint and by POST /v1/staff once the
// one-time credential has already been reported - see CreateStaffResponse.
public record StaffResponse(
    string StaffId,
    string FullName,
    string Username,
    string Role,
    string Status,
    bool MustChangePassword,
    string? SupervisorStaffId,
    DateTimeOffset CreatedAt,
    string? CreatedByStaffId,
    DateTimeOffset UpdatedAt);
