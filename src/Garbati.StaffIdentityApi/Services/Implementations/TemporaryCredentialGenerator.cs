using System.Security.Cryptography;

namespace Garbati.StaffIdentityApi.Services.Implementations;

// Generates the one-time temporary password DESIGN.md Section 3 (endpoints 2 and 8's admin branch) hands
// back once, in the response body, never persisted in plain text. Unlike StaffIdGenerator/
// TransactionReference-style public identifiers, this IS a secret, so it uses RandomNumberGenerator (CSPRNG),
// not Random.Shared. 20 characters from a 58-character alphabet (~117 bits of entropy) comfortably clears
// DESIGN.md Section 4's 12-character minimum - the policy applies to what a human chooses, not to what
// this generates, but there is no reason to generate something weaker than the policy it must satisfy.
public static class TemporaryCredentialGenerator
{
    private const string AllowedChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
    private const int Length = 20;

    public static string Generate()
    {
        var chars = new char[Length];
        for (var i = 0; i < Length; i++)
        {
            chars[i] = AllowedChars[RandomNumberGenerator.GetInt32(AllowedChars.Length)];
        }
        return new string(chars);
    }
}
