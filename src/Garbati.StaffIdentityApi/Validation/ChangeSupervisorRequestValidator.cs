using Garbati.StaffIdentityApi.Contracts.Requests;

namespace Garbati.StaffIdentityApi.Validation;

public class ChangeSupervisorRequestValidator
{
    public IReadOnlyList<string> Validate(ChangeSupervisorRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.ActingStaffId))
            errors.Add("ActingStaffId is required.");

        // SupervisorStaffId itself is allowed to be null (unassign) - whether it references a real,
        // Active, Supervisor-role staff member is a database-dependent business rule, checked in
        // Services/Implementations/StaffService.cs, not here.

        return errors;
    }
}
