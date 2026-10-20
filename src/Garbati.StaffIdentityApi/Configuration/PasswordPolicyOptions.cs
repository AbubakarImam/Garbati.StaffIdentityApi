using Microsoft.Extensions.Options;

namespace Garbati.StaffIdentityApi.Configuration;

// DESIGN.md Section 4: "Minimum 12 characters. No composition rules." Bound from the "PasswordPolicy"
// configuration section rather than hardcoded, so the one number this service enforces is visible and
// changeable without a code edit - matching how similar single-value policy knobs (e.g. page sizes) are
// kept in this platform's other services' validators/options rather than as magic numbers.
public class PasswordPolicyOptions
{
    public int MinimumLength { get; set; } = 12;
}

// Runs at startup (ValidateOnStart), same rationale as ApiKeyOptionsValidator: a nonsensical value fails
// fast instead of surfacing as a confusing validation error on the first real request.
public class PasswordPolicyOptionsValidator : IValidateOptions<PasswordPolicyOptions>
{
    public ValidateOptionsResult Validate(string? name, PasswordPolicyOptions options) =>
        options.MinimumLength < 12
            ? ValidateOptionsResult.Fail("PasswordPolicy:MinimumLength must be at least 12 (DESIGN.md Section 4).")
            : ValidateOptionsResult.Success;
}
