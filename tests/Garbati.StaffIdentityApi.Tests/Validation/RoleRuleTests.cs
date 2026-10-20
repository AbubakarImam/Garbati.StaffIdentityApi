using Garbati.StaffIdentityApi.Entities;
using Garbati.StaffIdentityApi.Validation;

namespace Garbati.StaffIdentityApi.Tests.Validation;

public class RoleRuleTests
{
    [Theory]
    [InlineData("Teller")]
    [InlineData("CustomerServiceOfficer")]
    [InlineData("Supervisor")]
    [InlineData("ComplianceOfficer")]
    [InlineData("RiskOfficer")]
    [InlineData("Auditor")]
    [InlineData("Administrator")]
    public void Validate_AcceptsEveryRoleInTheFixedVocabulary(string role)
    {
        Assert.Null(RoleRule.Validate(role, out var parsed));
        Assert.Equal(role, parsed.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_RejectsMissingRole(string? role)
    {
        Assert.NotNull(RoleRule.Validate(role, out _));
    }

    [Theory]
    [InlineData("teller")] // case-sensitive - not one of the exact seven names
    [InlineData("SuperAdmin")]
    public void Validate_RejectsUnknownRoleNames(string role)
    {
        Assert.NotNull(RoleRule.Validate(role, out _));
    }
}
