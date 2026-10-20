using Garbati.StaffIdentityApi.Configuration;
using Garbati.StaffIdentityApi.Contracts.Requests;
using Microsoft.Extensions.Options;

namespace Garbati.StaffIdentityApi.Validation;

public class ChangeStaffPasswordRequestValidator(IOptions<PasswordPolicyOptions> passwordPolicyOptions)
{
    private readonly PasswordPolicyOptions passwordPolicy = passwordPolicyOptions.Value;

    // Shape validation only (required fields, minimum length). Which fields actually matter depends on
    // the caller's scope - Controllers/StaffController.cs.ChangePassword decides that, and calls
    // ValidateSelfService below only on the self-service branch.
    public IReadOnlyList<string> ValidateAdmin(ChangeStaffPasswordRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.ActingStaffId))
            errors.Add("ActingStaffId is required.");

        return errors;
    }

    public IReadOnlyList<string> ValidateSelfService(ChangeStaffPasswordRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.ActingStaffId))
            errors.Add("ActingStaffId is required.");

        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            errors.Add("CurrentPassword is required.");

        if (string.IsNullOrWhiteSpace(request.NewPassword))
            errors.Add("NewPassword is required.");
        else if (request.NewPassword.Length < passwordPolicy.MinimumLength)
            errors.Add($"NewPassword must be at least {passwordPolicy.MinimumLength} characters (DESIGN.md Section 4).");

        return errors;
    }
}
