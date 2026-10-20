using System.Net;
using System.Net.Http.Json;
using Garbati.StaffIdentityApi.Authorization;
using Garbati.StaffIdentityApi.Contracts.Responses;
using Garbati.StaffIdentityApi.Tests.Fixtures;

namespace Garbati.StaffIdentityApi.Tests.Controllers;

// DESIGN.md Section 3, endpoints 3 and 4.
[Collection("Api")]
public class StaffQueryTests(ApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient AdminClient() => fixture.CreateClient(ApiFixture.OnlyClient(StaffIdentityScopes.Admin));

    [Fact]
    public async Task GetStaff_ReturnsTheStaffMember_WithoutAPasswordHashField_AndWithSupervisorStaffId()
    {
        using var admin = AdminClient();
        var supervisor = await StaffApiHelpers.CreateStaffAsync(admin, role: "Supervisor", username: "query-supervisor");
        var teller = await StaffApiHelpers.CreateStaffAsync(admin, role: "Teller", username: "query-teller");
        using var assign = await admin.PatchAsJsonAsync($"/v1/staff/{teller.Staff.StaffId}/supervisor",
            new { actingStaffId = "STF-TESTACTOR", supervisorStaffId = supervisor.Staff.StaffId });
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        using var response = await admin.GetAsync($"/v1/staff/{teller.Staff.StaffId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);

        var body = await response.Content.ReadFromJsonAsync<StaffResponse>();
        Assert.Equal(teller.Staff.StaffId, body!.StaffId);
        Assert.Equal(supervisor.Staff.StaffId, body.SupervisorStaffId);
    }

    [Fact]
    public async Task GetStaff_UnknownStaffId_Returns404()
    {
        using var admin = AdminClient();

        using var response = await admin.GetAsync("/v1/staff/STF-DOESNOTEXIST");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListStaff_WithoutFilter_ReturnsEveryStaffMember_Paged()
    {
        using var admin = AdminClient();
        await StaffApiHelpers.CreateStaffAsync(admin, username: "list-user-one");
        await StaffApiHelpers.CreateStaffAsync(admin, username: "list-user-two");

        using var response = await admin.GetAsync("/v1/staff?page=1&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StaffListResponse>();
        Assert.Equal(1, body!.Page);
        Assert.Equal(1, body.PageSize);
        Assert.Single(body.Items);
        // At least the seeded Administrator plus the two just created.
        Assert.True(body.TotalCount >= 3);
    }

    [Fact]
    public async Task ListStaff_WithSupervisorFilter_ReturnsOnlyThatSupervisorsReports()
    {
        using var admin = AdminClient();
        var supervisorA = await StaffApiHelpers.CreateStaffAsync(admin, role: "Supervisor", username: "filter-supervisor-a");
        var supervisorB = await StaffApiHelpers.CreateStaffAsync(admin, role: "Supervisor", username: "filter-supervisor-b");
        var reportToA = await StaffApiHelpers.CreateStaffAsync(admin, role: "Teller", username: "filter-report-a");
        var reportToB = await StaffApiHelpers.CreateStaffAsync(admin, role: "Teller", username: "filter-report-b");

        await admin.PatchAsJsonAsync($"/v1/staff/{reportToA.Staff.StaffId}/supervisor",
            new { actingStaffId = "STF-TESTACTOR", supervisorStaffId = supervisorA.Staff.StaffId });
        await admin.PatchAsJsonAsync($"/v1/staff/{reportToB.Staff.StaffId}/supervisor",
            new { actingStaffId = "STF-TESTACTOR", supervisorStaffId = supervisorB.Staff.StaffId });

        using var response = await admin.GetAsync($"/v1/staff?supervisorStaffId={supervisorA.Staff.StaffId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StaffListResponse>();
        Assert.Equal(1, body!.TotalCount);
        Assert.Equal(reportToA.Staff.StaffId, Assert.Single(body.Items).StaffId);
    }
}
