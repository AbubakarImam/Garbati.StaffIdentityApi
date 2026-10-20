namespace Garbati.StaffIdentityApi.Exceptions;

// DESIGN.md Section 2.4/3: supervisorStaffId must be null or reference an existing, Active,
// Role = Supervisor StaffUser, and may never be the staff member's own StaffId. Mapped to 400 (the
// design doc's own wording for this rule, not the platform's usual generic 422 business-rule bucket).
public class InvalidSupervisorAssignmentException : Exception
{
    public InvalidSupervisorAssignmentException()
    {
    }

    public InvalidSupervisorAssignmentException(string message)
        : base(message)
    {
    }

    public InvalidSupervisorAssignmentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
