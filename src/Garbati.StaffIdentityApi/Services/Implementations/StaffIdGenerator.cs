namespace Garbati.StaffIdentityApi.Services.Implementations;

// Generates the internal, human-readable StaffId (DESIGN.md Section 2.1: "STF-XXXXXXXXXX, same shape as
// IdentityApi's CUS-.../OrchestratorApi's OPN-..."). Same approach as CustomerIdGenerator/
// OperationIdGenerator: ambiguous characters (0/O, 1/I) are left out so a StaffId can be read aloud or
// typed from a screenshot. Not cryptographically sensitive - unlike TemporaryCredentialGenerator, this is
// an identifier, not a secret, so Random.Shared is fine (matches the sibling generators exactly).
public static class StaffIdGenerator
{
    private const string AllowedChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int Length = 10;
    private const string Prefix = "STF";

    public static string Generate()
    {
        var id = new char[Length];
        for (var i = 0; i < Length; i++)
        {
            id[i] = AllowedChars[Random.Shared.Next(AllowedChars.Length)];
        }
        return Prefix + "-" + new string(id);
    }
}
