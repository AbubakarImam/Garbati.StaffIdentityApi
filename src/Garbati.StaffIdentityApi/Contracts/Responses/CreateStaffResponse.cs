namespace Garbati.StaffIdentityApi.Contracts.Responses;

// DESIGN.md Section 3, endpoint 2: "a one-time temporary credential ... returned once in the response".
// TemporaryPassword is non-null only on the call that actually created the row - never on an idempotent
// replay of the same key+body (task file: "the one-time credential is never returned on a second
// (idempotent-replay) response the same way it was on the first"). The plaintext is never persisted, so
// a replay has no way to reconstruct it even if this wanted to.
public record CreateStaffResponse(StaffResponse Staff, string? TemporaryPassword);
