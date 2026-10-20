namespace Garbati.StaffIdentityApi.Exceptions;

public class StaffNotFoundException : Exception
{
    public StaffNotFoundException()
    {
    }

    public StaffNotFoundException(string message)
        : base(message)
    {
    }

    public StaffNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
