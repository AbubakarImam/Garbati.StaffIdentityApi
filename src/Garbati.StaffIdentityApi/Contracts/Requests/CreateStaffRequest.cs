namespace Garbati.StaffIdentityApi.Contracts.Requests;

// DESIGN.md Section 3, endpoint 2. No password field - a one-time temporary credential is generated
// server-side (Services/Implementations/TemporaryCredentialGenerator.cs). Deliberately no
// supervisorStaffId either (task file: "do NOT let POST /v1/staff set it at creation, DESIGN.md doesn't
// call for that") - PATCH .../supervisor is the only way to set it, always independently audited.
public record CreateStaffRequest(
    string IdempotencyKey,
    string ActingStaffId,
    string FullName,
    string Username,
    string Role) : IIdempotentRequest;
