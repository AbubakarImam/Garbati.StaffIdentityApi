namespace Garbati.StaffIdentityApi.Exceptions;

// POST /v1/staff/{staffId}/password, self-service branch (staffidentity.authenticate scope): the caller
// must supply and verify their own current password. A mismatch is a clear, explicit failure (400) - not
// a silent no-op, and not the generic authentication-401 shape (that status is reserved for the calling
// API client's own key, ARCHITECTURE.md Section 5 - this is a business-level check on a different,
// already-authenticated caller's own credential).
public class IncorrectCurrentPasswordException : Exception
{
    public IncorrectCurrentPasswordException()
    {
    }

    public IncorrectCurrentPasswordException(string message)
        : base(message)
    {
    }

    public IncorrectCurrentPasswordException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
