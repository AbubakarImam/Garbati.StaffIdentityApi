using System.Net;
using System.Net.Http.Json;
using Garbati.StaffIdentityApi.Authorization;
using Garbati.StaffIdentityApi.Contracts.Responses;
using Garbati.StaffIdentityApi.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Garbati.StaffIdentityApi.Tests.Controllers;

// DESIGN.md Section 3, endpoint 8: one route, two behaviors branched on the caller's own scope claim.
[Collection("Api")]
public class PasswordTests(ApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient AdminClient() => fixture.CreateClient(ApiFixture.OnlyClient(StaffIdentityScopes.Admin));
    private HttpClient AuthClient() => fixture.CreateClient(ApiFixture.OnlyClient(StaffIdentityScopes.Authenticate));

    [Fact]
    public async Task AdminBranch_ResetsThePassword_SetsMustChangePassword_AndReturnsAFreshOneTimeCredential()
    {
        using var admin = AdminClient();
        var staff = await StaffApiHelpers.CreateStaffAsync(admin, username: "pw-admin-reset-user");
        // Clear MustChangePassword first via a self-service change, so the test can actually observe the
        // admin reset setting it back to true rather than it merely staying true from creation.
        using var auth = AuthClient();
        await auth.PostAsJsonAsync($"/v1/staff/{staff.Staff.StaffId}/password",
            new { actingStaffId = staff.Staff.StaffId, currentPassword = staff.TemporaryPassword, newPassword = "a-strong-enough-password-1" });

        using var response = await admin.PostAsJsonAsync($"/v1/staff/{staff.Staff.StaffId}/password",
            new { actingStaffId = "STF-TESTACTOR" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ChangePasswordResponse>();
        Assert.NotNull(body!.TemporaryPassword);

        await using var ctx = fixture.CreateContext();
        var reloaded = await ctx.StaffUsers.AsNoTracking().FirstAsync(s => s.StaffId == staff.Staff.StaffId);
        Assert.True(reloaded.MustChangePassword);

        var auditRow = await ctx.StaffAuditLogs.AsNoTracking()
            .Where(a => a.TargetStaffId == staff.Staff.StaffId && a.Action == "PasswordReset")
            .OrderByDescending(a => a.Id).FirstOrDefaultAsync();
        Assert.NotNull(auditRow);
        Assert.Equal("STF-TESTACTOR", auditRow!.ActingStaffId);

        // The new credential actually works.
        using var authResponse = await AuthClient().PostAsJsonAsync("/v1/staff/authenticate",
            new { username = "pw-admin-reset-user", password = body.TemporaryPassword });
        Assert.Equal(HttpStatusCode.OK, authResponse.StatusCode);
    }

    [Fact]
    public async Task SelfServiceBranch_RequiresAndVerifiesCurrentPassword_AndClearsMustChangePassword()
    {
        using var admin = AdminClient();
        var staff = await StaffApiHelpers.CreateStaffAsync(admin, username: "pw-selfservice-user");

        using var auth = AuthClient();
        using var response = await auth.PostAsJsonAsync($"/v1/staff/{staff.Staff.StaffId}/password",
            new { actingStaffId = staff.Staff.StaffId, currentPassword = staff.TemporaryPassword, newPassword = "a-brand-new-password-1" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ChangePasswordResponse>();
        Assert.Null(body!.TemporaryPassword);

        await using var ctx = fixture.CreateContext();
        var reloaded = await ctx.StaffUsers.AsNoTracking().FirstAsync(s => s.StaffId == staff.Staff.StaffId);
        Assert.False(reloaded.MustChangePassword);

        using var authResponse = await auth.PostAsJsonAsync("/v1/staff/authenticate",
            new { username = "pw-selfservice-user", password = "a-brand-new-password-1" });
        Assert.Equal(HttpStatusCode.OK, authResponse.StatusCode);
    }

    [Fact]
    public async Task SelfServiceBranch_WrongCurrentPassword_FailsClearly_AndDoesNotChangeAnything()
    {
        using var admin = AdminClient();
        var staff = await StaffApiHelpers.CreateStaffAsync(admin, username: "pw-wrongcurrent-user");

        using var auth = AuthClient();
        using var response = await auth.PostAsJsonAsync($"/v1/staff/{staff.Staff.StaffId}/password",
            new { actingStaffId = staff.Staff.StaffId, currentPassword = "not-the-real-current-password", newPassword = "a-brand-new-password-1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // The original credential still works - nothing silently changed.
        using var authResponse = await auth.PostAsJsonAsync("/v1/staff/authenticate",
            new { username = "pw-wrongcurrent-user", password = staff.TemporaryPassword });
        Assert.Equal(HttpStatusCode.OK, authResponse.StatusCode);
    }

    [Fact]
    public async Task SelfServiceBranch_MissingNewPassword_Returns400()
    {
        using var admin = AdminClient();
        var staff = await StaffApiHelpers.CreateStaffAsync(admin, username: "pw-missingnew-user");

        using var auth = AuthClient();
        using var response = await auth.PostAsJsonAsync($"/v1/staff/{staff.Staff.StaffId}/password",
            new { actingStaffId = staff.Staff.StaffId, currentPassword = staff.TemporaryPassword });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NoApiKey_Returns401()
    {
        using var client = fixture.CreateClient(clientId: null);

        using var response = await client.PostAsJsonAsync("/v1/staff/STF-DOESNOTEXIST/password", new { actingStaffId = "STF-TESTACTOR" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
