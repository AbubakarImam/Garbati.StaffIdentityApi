namespace Garbati.StaffIdentityApi.Exceptions;

// DESIGN.md Section 3: "Rejects any transition out of Terminated (400)." Terminated is a true dead end -
// no code path in Services/Implementations/StaffService.cs ever moves a Terminated account back to
// Active or Suspended.
public class InvalidStatusTransitionException : Exception
{
    public InvalidStatusTransitionException()
    {
    }

    public InvalidStatusTransitionException(string message)
        : base(message)
    {
    }

    public InvalidStatusTransitionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
