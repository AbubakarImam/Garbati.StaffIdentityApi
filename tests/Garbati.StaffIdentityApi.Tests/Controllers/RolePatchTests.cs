using System.Net;
using System.Net.Http.Json;
using Garbati.StaffIdentityApi.Authorization;
using Garbati.StaffIdentityApi.Contracts.Responses;
using Garbati.StaffIdentityApi.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Garbati.StaffIdentityApi.Tests.Controllers;

// DESIGN.md Section 3, endpoint 5.
[Collection("Api")]
public class RolePatchTests(ApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient AdminClient() => fixture.CreateClient(ApiFixture.OnlyClient(StaffIdentityScopes.Admin));

    [Fact]
    public async Task ChangeRole_UpdatesTheRole_AndAuditsIndependently()
    {
        using var admin = AdminClient();
        var staff = await StaffApiHelpers.CreateStaffAsync(admin, role: "Teller", username: "role-change-user");

        using var response = await admin.PatchAsJsonAsync($"/v1/staff/{staff.Staff.StaffId}/role",
            new { actingStaffId = "STF-TESTACTOR", role = "CustomerServiceOfficer" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StaffResponse>();
        Assert.Equal("CustomerServiceOfficer", body!.Role);

        await using var ctx = fixture.CreateContext();
        var auditRow = await ctx.StaffAuditLogs.AsNoTracking()
            .Where(a => a.TargetStaffId == staff.Staff.StaffId && a.Action == "RoleChanged")
            .FirstOrDefaultAsync();
        Assert.NotNull(auditRow);
    }

    // DESIGN.md Section 3, endpoint 5: changing role away from Teller/CustomerServiceOfficer does NOT
    // touch SupervisorStaffId - that is PATCH .../supervisor's separate, independently-audited job.
    [Fact]
    public async Task ChangeRole_DoesNotTouchSupervisorStaffId_EvenWhenTheNewRoleIsNotASupervisedLineRole()
    {
        using var admin = AdminClient();
        var supervisor = await StaffApiHelpers.CreateStaffAsync(admin, role: "Supervisor", username: "role-untouched-supervisor");
        var staff = await StaffApiHelpers.CreateStaffAsync(admin, role: "Teller", username: "role-untouched-user");
        await admin.PatchAsJsonAsync($"/v1/staff/{staff.Staff.StaffId}/supervisor",
            new { actingStaffId = "STF-TESTACTOR", supervisorStaffId = supervisor.Staff.StaffId });

        using var response = await admin.PatchAsJsonAsync($"/v1/staff/{staff.Staff.StaffId}/role",
            new { actingStaffId = "STF-TESTACTOR", role = "ComplianceOfficer" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StaffResponse>();
        Assert.Equal("ComplianceOfficer", body!.Role);
        Assert.Equal(supervisor.Staff.StaffId, body.SupervisorStaffId);
    }

    [Fact]
    public async Task ChangeRole_UnknownStaffId_Returns404()
    {
        using var admin = AdminClient();

        using var response = await admin.PatchAsJsonAsync("/v1/staff/STF-DOESNOTEXIST/role",
            new { actingStaffId = "STF-TESTACTOR", role = "Teller" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangeRole_InvalidRole_Returns400()
    {
        using var admin = AdminClient();
        var staff = await StaffApiHelpers.CreateStaffAsync(admin, username: "role-badvalue-user");

        using var response = await admin.PatchAsJsonAsync($"/v1/staff/{staff.Staff.StaffId}/role",
            new { actingStaffId = "STF-TESTACTOR", role = "NotARealRole" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
