using Garbati.StaffIdentityApi.Contracts.Requests;

namespace Garbati.StaffIdentityApi.Validation;

public class AuthenticateRequestValidator
{
    public IReadOnlyList<string> Validate(AuthenticateRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Username))
            errors.Add("Username is required.");
        if (string.IsNullOrWhiteSpace(request.Password))
            errors.Add("Password is required.");

        return errors;
    }
}
