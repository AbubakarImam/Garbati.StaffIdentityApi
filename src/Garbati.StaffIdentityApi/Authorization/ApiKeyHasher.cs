using System.Security.Cryptography;
using System.Text;

namespace Garbati.StaffIdentityApi.Authorization;

// Identical to LedgerApi's/IdentityApi's/OrchestratorApi's ApiKeyHasher: keys are random, high-entropy
// values, so a plain SHA-256 is enough; slow hashes (bcrypt/PBKDF2) are for low-entropy secrets like
// passwords (see Services/Implementations and the PasswordHasher<StaffUser> usage there instead).
public static class ApiKeyHasher
{
    public static byte[] Hash(string apiKey) => SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));

    // Configured hashes are base64; a valid one decodes to exactly 32 bytes (SHA-256).
    public static bool TryDecode(string? base64Hash, out byte[] hash)
    {
        hash = [];
        if (string.IsNullOrWhiteSpace(base64Hash)) return false;
        try
        {
            hash = Convert.FromBase64String(base64Hash);
            return hash.Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
