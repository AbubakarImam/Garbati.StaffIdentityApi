namespace Garbati.StaffIdentityApi.Exceptions;

// Username uniqueness is enforced by a real database-level constraint (Data/StaffIdentityDbContext.cs'
// unique index on NormalizedUsername) - this exception is thrown when that constraint is the one that
// actually rejected an insert, surfaced as 409 (an already-existing resource conflicts with this request),
// not a generic 500.
public class DuplicateUsernameException : Exception
{
    public DuplicateUsernameException()
    {
    }

    public DuplicateUsernameException(string message)
        : base(message)
    {
    }

    public DuplicateUsernameException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
