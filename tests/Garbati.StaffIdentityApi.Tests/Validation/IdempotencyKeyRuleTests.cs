using Garbati.StaffIdentityApi.Validation;

namespace Garbati.StaffIdentityApi.Tests.Validation;

public class IdempotencyKeyRuleTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsMissingKeys(string? key)
    {
        Assert.NotNull(IdempotencyKeyRule.Validate(key));
    }

    [Theory]
    [InlineData("has a space")]
    [InlineData("has/a/slash")]
    public void Validate_RejectsDisallowedCharacters(string key)
    {
        Assert.NotNull(IdempotencyKeyRule.Validate(key));
    }

    [Fact]
    public void Validate_RejectsKeysLongerThanMaxLength()
    {
        var key = new string('a', IdempotencyKeyRule.MaxLength + 1);
        Assert.NotNull(IdempotencyKeyRule.Validate(key));
    }

    [Theory]
    [InlineData("valid-key_123.abc:def")]
    [InlineData("a")]
    public void Validate_AcceptsWellFormedKeys(string key)
    {
        Assert.Null(IdempotencyKeyRule.Validate(key));
    }
}
