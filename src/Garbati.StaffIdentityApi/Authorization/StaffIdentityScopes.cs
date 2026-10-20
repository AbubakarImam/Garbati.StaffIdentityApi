namespace Garbati.StaffIdentityApi.Authorization;

// Each scope is also the name of the authorization policy that requires it (same pattern as
// LedgerApi.Authorization.LedgerScopes / Garbati.IdentityApi.Authorization.IdentityScopes /
// Garbati.OrchestratorApi.Authorization.OrchestratorScopes).
//
// DESIGN.md Section 3 decision #3: exactly two scopes, deliberately kept separate even though OpsApi (the
// one caller) needs both - "authenticate" and "administer" are genuinely different blast radii for the
// same caller, unlike OrchestratorApi's single-scope-per-caller judgment call elsewhere in this platform.
public static class StaffIdentityScopes
{
    public const string ClaimType = "scope";

    public const string Authenticate = "staffidentity.authenticate";
    public const string Admin = "staffidentity.admin";

    public static readonly IReadOnlyList<string> All = [Authenticate, Admin];
}
