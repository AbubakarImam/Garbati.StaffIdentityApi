using Garbati.StaffIdentityApi.Entities;

namespace Garbati.StaffIdentityApi.Validation;

// Shared by every request validator that takes a Role by name (DESIGN.md Section 2.2's fixed vocabulary).
public static class RoleRule
{
    public static string? Validate(string? role, out Role parsed)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            parsed = default;
            return "Role is required.";
        }

        if (!Enum.TryParse(role, ignoreCase: false, out parsed))
        {
            return $"Role must be one of: {string.Join(", ", Enum.GetNames<Role>())}.";
        }

        return null;
    }
}
