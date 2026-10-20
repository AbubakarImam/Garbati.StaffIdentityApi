using System.Text.RegularExpressions;

namespace Garbati.StaffIdentityApi.Validation;

// Same rule as LedgerApi's/IdentityApi's/OrchestratorApi's IdempotencyKeyRule: short enough to always fit
// a unique index, limited to characters that are safe in logs and URLs. This service derives no further
// downstream idempotency keys of its own (it calls nothing - DESIGN.md Section 1), so unlike
// OrchestratorApi's version there is no reserved prefix to police here.
public static partial class IdempotencyKeyRule
{
    public const int MaxLength = 128;

    // \z, not $: in .NET, $ also matches just before a trailing newline, so "abc\n" would pass.
    [GeneratedRegex(@"^[A-Za-z0-9._:-]{1,128}\z")]
    private static partial Regex Pattern();

    public static string? Validate(string? key) =>
        string.IsNullOrWhiteSpace(key) ? "IdempotencyKey is required."
        : !Pattern().IsMatch(key) ? $"IdempotencyKey must be 1-{MaxLength} characters: letters, digits, '-', '_', '.' or ':'."
        : null;
}
