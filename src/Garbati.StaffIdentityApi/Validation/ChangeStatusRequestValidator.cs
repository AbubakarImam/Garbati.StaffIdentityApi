using Garbati.StaffIdentityApi.Contracts.Requests;
using Garbati.StaffIdentityApi.Entities;

namespace Garbati.StaffIdentityApi.Validation;

public class ChangeStatusRequestValidator
{
    public IReadOnlyList<string> Validate(ChangeStatusRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.ActingStaffId))
            errors.Add("ActingStaffId is required.");

        if (string.IsNullOrWhiteSpace(request.Status))
            errors.Add("Status is required.");
        else if (!Enum.TryParse<StaffStatus>(request.Status, ignoreCase: false, out _))
            errors.Add($"Status must be one of: {string.Join(", ", Enum.GetNames<StaffStatus>())}.");

        return errors;
    }
}
