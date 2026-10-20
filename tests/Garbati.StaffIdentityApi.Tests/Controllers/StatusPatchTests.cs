using System.Net;
using System.Net.Http.Json;
using Garbati.StaffIdentityApi.Authorization;
using Garbati.StaffIdentityApi.Contracts.Responses;
using Garbati.StaffIdentityApi.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Garbati.StaffIdentityApi.Tests.Controllers;

// DESIGN.md Section 3, endpoint 6.
[Collection("Api")]
public class StatusPatchTests(ApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient AdminClient() => fixture.CreateClient(ApiFixture.OnlyClient(StaffIdentityScopes.Admin));

    [Fact]
    public async Task ChangeStatus_SuspendsAnActiveAccount_AndAudits()
    {
        using var admin = AdminClient();
        var staff = await StaffApiHelpers.CreateStaffAsync(admin, username: "status-suspend-user");

        using var response = await admin.PatchAsJsonAsync($"/v1/staff/{staff.Staff.StaffId}/status",
            new { actingStaffId = "STF-TESTACTOR", status = "Suspended" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StaffResponse>();
        Assert.Equal("Suspended", body!.Status);

        await using var ctx = fixture.CreateContext();
        var auditRow = await ctx.StaffAuditLogs.AsNoTracking()
            .Where(a => a.TargetStaffId == staff.Staff.StaffId && a.Action == "StatusChanged")
            .FirstOrDefaultAsync();
        Assert.NotNull(auditRow);
    }

    [Fact]
    public async Task ChangeStatus_ReactivatesASuspendedAccount()
    {
        using var admin = AdminClient();
        var staff = await StaffApiHelpers.CreateStaffAsync(admin, username: "status-reactivate-user");
        await StaffApiHelpers.SetStatusAsync(admin, staff.Staff.StaffId, "Suspended");

        using var response = await admin.PatchAsJsonAsync($"/v1/staff/{staff.Staff.StaffId}/status",
            new { actingStaffId = "STF-TESTACTOR", status = "Active" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StaffResponse>();
        Assert.Equal("Active", body!.Status);
    }

    // DESIGN.md Section 3, endpoint 6: Terminated is a true dead end. Every other status as a source,
    // attempting to leave Terminated, must be rejected (400).
    [Theory]
    [InlineData("Active")]
    [InlineData("Suspended")]
    public async Task ChangeStatus_RejectsAnyTransitionOutOfTerminated(string attemptedNewStatus)
    {
        using var admin = AdminClient();
        var staff = await StaffApiHelpers.CreateStaffAsync(admin, username: $"status-terminated-{attemptedNewStatus.ToLowerInvariant()}");
        await StaffApiHelpers.SetStatusAsync(admin, staff.Staff.StaffId, "Terminated");

        using var response = await admin.PatchAsJsonAsync($"/v1/staff/{staff.Staff.StaffId}/status",
            new { actingStaffId = "STF-TESTACTOR", status = attemptedNewStatus });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var ctx = fixture.CreateContext();
        var reloaded = await ctx.StaffUsers.AsNoTracking().FirstAsync(s => s.StaffId == staff.Staff.StaffId);
        Assert.Equal("Terminated", reloaded.Status.ToString());
    }

    [Fact]
    public async Task ChangeStatus_TerminatedToTerminated_IsAnIdempotentNoOp_NotARejectedTransition()
    {
        using var admin = AdminClient();
        var staff = await StaffApiHelpers.CreateStaffAsync(admin, username: "status-term-to-term-user");
        await StaffApiHelpers.SetStatusAsync(admin, staff.Staff.StaffId, "Terminated");

        using var response = await admin.PatchAsJsonAsync($"/v1/staff/{staff.Staff.StaffId}/status",
            new { actingStaffId = "STF-TESTACTOR", status = "Terminated" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_UnknownStaffId_Returns404()
    {
        using var admin = AdminClient();

        using var response = await admin.PatchAsJsonAsync("/v1/staff/STF-DOESNOTEXIST/status",
            new { actingStaffId = "STF-TESTACTOR", status = "Suspended" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_InvalidStatus_Returns400()
    {
        using var admin = AdminClient();
        var staff = await StaffApiHelpers.CreateStaffAsync(admin, username: "status-badvalue-user");

        using var response = await admin.PatchAsJsonAsync($"/v1/staff/{staff.Staff.StaffId}/status",
            new { actingStaffId = "STF-TESTACTOR", status = "OnLeave" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
