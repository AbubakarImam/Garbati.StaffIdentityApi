using Garbati.StaffIdentityApi.Contracts.Requests;

namespace Garbati.StaffIdentityApi.Validation;

public class CreateStaffRequestValidator
{
    public IReadOnlyList<string> Validate(CreateStaffRequest request)
    {
        var errors = new List<string>();

        if (IdempotencyKeyRule.Validate(request.IdempotencyKey) is { } idempotencyError)
            errors.Add(idempotencyError);

        if (string.IsNullOrWhiteSpace(request.ActingStaffId))
            errors.Add("ActingStaffId is required.");

        if (string.IsNullOrWhiteSpace(request.FullName))
            errors.Add("FullName is required.");

        if (string.IsNullOrWhiteSpace(request.Username))
            errors.Add("Username is required.");

        if (RoleRule.Validate(request.Role, out _) is { } roleError)
            errors.Add(roleError);

        return errors;
    }
}
