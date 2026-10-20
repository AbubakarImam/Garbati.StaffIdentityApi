using System.Net;
using System.Net.Http.Json;
using Garbati.StaffIdentityApi.Authorization;
using Garbati.StaffIdentityApi.Contracts.Responses;
using Garbati.StaffIdentityApi.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Garbati.StaffIdentityApi.Tests.Controllers;

// DESIGN.md Section 3, endpoint 2.
[Collection("Api")]
public class CreateStaffTests(ApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient AdminClient() => fixture.CreateClient(ApiFixture.OnlyClient(StaffIdentityScopes.Admin));

    [Fact]
    public async Task CreateStaff_HappyPath_ReturnsTheStaffAndAOneTimeCredential()
    {
        using var admin = AdminClient();

        var request = new
        {
            idempotencyKey = $"create-happy-{Guid.NewGuid():N}",
            actingStaffId = "STF-TESTACTOR",
            fullName = "Happy Path Staff",
            username = "happy-path-user",
            role = "Teller",
        };

        using var response = await admin.PostAsJsonAsync("/v1/staff", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateStaffResponse>();
        Assert.NotNull(body!.TemporaryPassword);
        Assert.True(body.TemporaryPassword!.Length >= 12);
        Assert.Equal("happy-path-user", body.Staff.Username);
        Assert.Equal("Teller", body.Staff.Role);
        Assert.Equal("Active", body.Staff.Status);
        Assert.True(body.Staff.MustChangePassword);
        Assert.Null(body.Staff.SupervisorStaffId);
        Assert.Equal("STF-TESTACTOR", body.Staff.CreatedByStaffId);

        await using var ctx = fixture.CreateContext();
        var auditRow = await ctx.StaffAuditLogs.AsNoTracking()
            .Where(a => a.TargetStaffId == body.Staff.StaffId && a.Action == "StaffCreated")
            .FirstOrDefaultAsync();
        Assert.NotNull(auditRow);
        Assert.Equal("STF-TESTACTOR", auditRow!.ActingStaffId);
    }

    [Fact]
    public async Task CreateStaff_RetryWithTheSameKeyAndBody_ReturnsTheSameStaff_WithoutTheCredentialAgain()
    {
        using var admin = AdminClient();
        var key = $"create-retry-{Guid.NewGuid():N}";
        var request = new
        {
            idempotencyKey = key,
            actingStaffId = "STF-TESTACTOR",
            fullName = "Retry Staff",
            username = "retry-user",
            role = "Teller",
        };

        using var first = await admin.PostAsJsonAsync("/v1/staff", request);
        var firstBody = await first.Content.ReadFromJsonAsync<CreateStaffResponse>();

        using var second = await admin.PostAsJsonAsync("/v1/staff", request);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<CreateStaffResponse>();

        Assert.Equal(firstBody!.Staff.StaffId, secondBody!.Staff.StaffId);
        Assert.NotNull(firstBody.TemporaryPassword);
        // Task file: the one-time credential is never returned on an idempotent replay.
        Assert.Null(secondBody.TemporaryPassword);

        await using var ctx = fixture.CreateContext();
        var count = await ctx.StaffUsers.CountAsync(s => s.Username == "retry-user");
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CreateStaff_SameKeyDifferentBody_Returns409()
    {
        using var admin = AdminClient();
        var key = $"create-conflict-{Guid.NewGuid():N}";

        using var first = await admin.PostAsJsonAsync("/v1/staff", new
        {
            idempotencyKey = key,
            actingStaffId = "STF-TESTACTOR",
            fullName = "Original Staff",
            username = "conflict-user-one",
            role = "Teller",
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await admin.PostAsJsonAsync("/v1/staff", new
        {
            idempotencyKey = key,
            actingStaffId = "STF-TESTACTOR",
            fullName = "Different Staff",
            username = "conflict-user-two",
            role = "Supervisor",
        });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task CreateStaff_DuplicateUsername_CaseInsensitive_Returns409()
    {
        using var admin = AdminClient();

        using var first = await admin.PostAsJsonAsync("/v1/staff", new
        {
            idempotencyKey = $"create-dupe-a-{Guid.NewGuid():N}",
            actingStaffId = "STF-TESTACTOR",
            fullName = "First Staff",
            username = "CaseTest.User",
            role = "Teller",
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await admin.PostAsJsonAsync("/v1/staff", new
        {
            idempotencyKey = $"create-dupe-b-{Guid.NewGuid():N}",
            actingStaffId = "STF-TESTACTOR",
            fullName = "Second Staff",
            // Same username, different case - must still collide (DB-level case-insensitive uniqueness).
            username = "casetest.user",
            role = "Teller",
        });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task CreateStaff_MissingIdempotencyKey_Returns400()
    {
        using var admin = AdminClient();

        using var response = await admin.PostAsJsonAsync("/v1/staff", new
        {
            idempotencyKey = "",
            actingStaffId = "STF-TESTACTOR",
            fullName = "No Key Staff",
            username = "no-key-user",
            role = "Teller",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateStaff_InvalidRole_Returns400()
    {
        using var admin = AdminClient();

        using var response = await admin.PostAsJsonAsync("/v1/staff", new
        {
            idempotencyKey = $"create-badrole-{Guid.NewGuid():N}",
            actingStaffId = "STF-TESTACTOR",
            fullName = "Bad Role Staff",
            username = "bad-role-user",
            role = "SuperUser",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
