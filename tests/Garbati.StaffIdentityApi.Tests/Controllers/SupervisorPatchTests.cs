using System.Net;
using System.Net.Http.Json;
using Garbati.StaffIdentityApi.Authorization;
using Garbati.StaffIdentityApi.Contracts.Responses;
using Garbati.StaffIdentityApi.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Garbati.StaffIdentityApi.Tests.Controllers;

// DESIGN.md Section 3, endpoint 7 and Section 2.4 (decision #4).
[Collection("Api")]
public class SupervisorPatchTests(ApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient AdminClient() => fixture.CreateClient(ApiFixture.OnlyClient(StaffIdentityScopes.Admin));

    [Fact]
    public async Task ChangeSupervisor_AssignsAValidActiveSupervisor_AndAudits()
    {
        using var admin = AdminClient();
        var supervisor = await StaffApiHelpers.CreateStaffAsync(admin, role: "Supervisor", username: "sup-assign-supervisor");
        var teller = await StaffApiHelpers.CreateStaffAsync(admin, role: "Teller", username: "sup-assign-teller");

        using var response = await admin.PatchAsJsonAsync($"/v1/staff/{teller.Staff.StaffId}/supervisor",
            new { actingStaffId = "STF-TESTACTOR", supervisorStaffId = supervisor.Staff.StaffId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StaffResponse>();
        Assert.Equal(supervisor.Staff.StaffId, body!.SupervisorStaffId);

        await using var ctx = fixture.CreateContext();
        var auditRow = await ctx.StaffAuditLogs.AsNoTracking()
            .Where(a => a.TargetStaffId == teller.Staff.StaffId && a.Action == "SupervisorChanged")
            .FirstOrDefaultAsync();
        Assert.NotNull(auditRow);
    }

    [Fact]
    public async Task ChangeSupervisor_Unassign_SetsSupervisorStaffIdToNull()
    {
        using var admin = AdminClient();
        var supervisor = await StaffApiHelpers.CreateStaffAsync(admin, role: "Supervisor", username: "sup-unassign-supervisor");
        var teller = await StaffApiHelpers.CreateStaffAsync(admin, role: "Teller", username: "sup-unassign-teller");
        await admin.PatchAsJsonAsync($"/v1/staff/{teller.Staff.StaffId}/supervisor",
            new { actingStaffId = "STF-TESTACTOR", supervisorStaffId = supervisor.Staff.StaffId });

        using var response = await admin.PatchAsJsonAsync($"/v1/staff/{teller.Staff.StaffId}/supervisor",
            new { actingStaffId = "STF-TESTACTOR", supervisorStaffId = (string?)null });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StaffResponse>();
        Assert.Null(body!.SupervisorStaffId);
    }

    [Fact]
    public async Task ChangeSupervisor_RejectsANonSupervisorTarget()
    {
        using var admin = AdminClient();
        var notASupervisor = await StaffApiHelpers.CreateStaffAsync(admin, role: "ComplianceOfficer", username: "sup-reject-notsupervisor");
        var teller = await StaffApiHelpers.CreateStaffAsync(admin, role: "Teller", username: "sup-reject-teller-a");

        using var response = await admin.PatchAsJsonAsync($"/v1/staff/{teller.Staff.StaffId}/supervisor",
            new { actingStaffId = "STF-TESTACTOR", supervisorStaffId = notASupervisor.Staff.StaffId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeSupervisor_RejectsAnAdministratorTarget_EvenThoughAdministratorIsAPrivilegedRole()
    {
        using var admin = AdminClient();
        var teller = await StaffApiHelpers.CreateStaffAsync(admin, role: "Teller", username: "sup-reject-teller-admin-target");

        using var response = await admin.PatchAsJsonAsync($"/v1/staff/{teller.Staff.StaffId}/supervisor",
            new { actingStaffId = "STF-TESTACTOR", supervisorStaffId = ApiFixture.SeededAdministratorStaffId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeSupervisor_RejectsASuspendedSupervisor()
    {
        using var admin = AdminClient();
        var supervisor = await StaffApiHelpers.CreateStaffAsync(admin, role: "Supervisor", username: "sup-reject-suspended-supervisor");
        await StaffApiHelpers.SetStatusAsync(admin, supervisor.Staff.StaffId, "Suspended");
        var teller = await StaffApiHelpers.CreateStaffAsync(admin, role: "Teller", username: "sup-reject-teller-b");

        using var response = await admin.PatchAsJsonAsync($"/v1/staff/{teller.Staff.StaffId}/supervisor",
            new { actingStaffId = "STF-TESTACTOR", supervisorStaffId = supervisor.Staff.StaffId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeSupervisor_RejectsSelfReference()
    {
        using var admin = AdminClient();
        var supervisor = await StaffApiHelpers.CreateStaffAsync(admin, role: "Supervisor", username: "sup-reject-self");

        using var response = await admin.PatchAsJsonAsync($"/v1/staff/{supervisor.Staff.StaffId}/supervisor",
            new { actingStaffId = "STF-TESTACTOR", supervisorStaffId = supervisor.Staff.StaffId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeSupervisor_RejectsAnUnknownSupervisorStaffId()
    {
        using var admin = AdminClient();
        var teller = await StaffApiHelpers.CreateStaffAsync(admin, role: "Teller", username: "sup-reject-teller-unknown");

        using var response = await admin.PatchAsJsonAsync($"/v1/staff/{teller.Staff.StaffId}/supervisor",
            new { actingStaffId = "STF-TESTACTOR", supervisorStaffId = "STF-DOESNOTEXIST" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
