using Garbati.StaffIdentityApi.Contracts.Requests;

namespace Garbati.StaffIdentityApi.Validation;

public class ChangeRoleRequestValidator
{
    public IReadOnlyList<string> Validate(ChangeRoleRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.ActingStaffId))
            errors.Add("ActingStaffId is required.");

        if (RoleRule.Validate(request.Role, out _) is { } roleError)
            errors.Add(roleError);

        return errors;
    }
}
