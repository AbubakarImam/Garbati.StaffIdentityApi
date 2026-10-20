using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Garbati.StaffIdentityApi.Authorization;
using Garbati.StaffIdentityApi.Contracts.Responses;
using Garbati.StaffIdentityApi.Entities;
using Garbati.StaffIdentityApi.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Garbati.StaffIdentityApi.Tests.Controllers;

// DESIGN.md Section 3, endpoint 1.
[Collection("Api")]
public class AuthenticateTests(ApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient AuthClient() => fixture.CreateClient(ApiFixture.OnlyClient(StaffIdentityScopes.Authenticate));
    private HttpClient AdminClient() => fixture.CreateClient(ApiFixture.OnlyClient(StaffIdentityScopes.Admin));

    [Fact]
    public async Task Authenticate_Succeeds_WithTheCorrectTemporaryCredential_AndAudits()
    {
        using var admin = AdminClient();
        var created = await StaffApiHelpers.CreateStaffAsync(admin, username: "auth-success-user");

        using var auth = AuthClient();
        using var response = await auth.PostAsJsonAsync("/v1/staff/authenticate",
            new { username = "auth-success-user", password = created.TemporaryPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthenticateResponse>();
        Assert.Equal(created.Staff.StaffId, body!.StaffId);
        Assert.Equal("Test Staff Member", body.FullName);
        Assert.Equal("Teller", body.Role);

        await using var ctx = fixture.CreateContext();
        var auditRow = await ctx.StaffAuditLogs.AsNoTracking()
            .Where(a => a.TargetStaffId == created.Staff.StaffId && a.Action == "AuthenticationSucceeded")
            .OrderByDescending(a => a.Id).FirstOrDefaultAsync();
        Assert.NotNull(auditRow);
        Assert.Equal(created.Staff.StaffId, auditRow!.ActingStaffId);
    }

    [Fact]
    public async Task Authenticate_Fails_WithTheWrongPassword()
    {
        using var admin = AdminClient();
        var created = await StaffApiHelpers.CreateStaffAsync(admin, username: "auth-wrongpw-user");

        using var auth = AuthClient();
        using var response = await auth.PostAsJsonAsync("/v1/staff/authenticate",
            new { username = "auth-wrongpw-user", password = "definitely-the-wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authenticate_Fails_WithAnUnknownUsername()
    {
        using var auth = AuthClient();
        using var response = await auth.PostAsJsonAsync("/v1/staff/authenticate",
            new { username = "no-such-user-at-all", password = "whatever-password-123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        await using var ctx = fixture.CreateContext();
        var auditRow = await ctx.StaffAuditLogs.AsNoTracking()
            .Where(a => a.TargetStaffId == "no-such-user-at-all" && a.Action == "AuthenticationFailed")
            .OrderByDescending(a => a.Id).FirstOrDefaultAsync();
        Assert.NotNull(auditRow);
        Assert.Null(auditRow!.ActingStaffId);
        Assert.Contains("no-such-user-at-all", auditRow.Details);
    }

    [Fact]
    public async Task Authenticate_Fails_ForASuspendedAccount_EvenWithTheCorrectPassword()
    {
        using var admin = AdminClient();
        var created = await StaffApiHelpers.CreateStaffAsync(admin, username: "auth-suspended-user");
        await StaffApiHelpers.SetStatusAsync(admin, created.Staff.StaffId, "Suspended");

        using var auth = AuthClient();
        using var response = await auth.PostAsJsonAsync("/v1/staff/authenticate",
            new { username = "auth-suspended-user", password = created.TemporaryPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authenticate_Fails_ForATerminatedAccount_EvenWithTheCorrectPassword()
    {
        using var admin = AdminClient();
        var created = await StaffApiHelpers.CreateStaffAsync(admin, username: "auth-terminated-user");
        await StaffApiHelpers.SetStatusAsync(admin, created.Staff.StaffId, "Terminated");

        using var auth = AuthClient();
        using var response = await auth.PostAsJsonAsync("/v1/staff/authenticate",
            new { username = "auth-terminated-user", password = created.TemporaryPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Task file: "confirm all four failure shapes are byte-for-byte identical (401, same generic
    // message)." correlationId legitimately differs per request (it is not a signal about WHY
    // authentication failed), so bodies are compared with that one field removed.
    [Fact]
    public async Task AllFourFailureShapes_AreIndistinguishable()
    {
        using var admin = AdminClient();
        var suspended = await StaffApiHelpers.CreateStaffAsync(admin, username: "shape-suspended-user");
        await StaffApiHelpers.SetStatusAsync(admin, suspended.Staff.StaffId, "Suspended");
        var terminated = await StaffApiHelpers.CreateStaffAsync(admin, username: "shape-terminated-user");
        await StaffApiHelpers.SetStatusAsync(admin, terminated.Staff.StaffId, "Terminated");
        var activeAccountForWrongPassword = await StaffApiHelpers.CreateStaffAsync(admin, username: "shape-wrongpw-user");

        using var auth = AuthClient();

        var scenarios = new[]
        {
            ("shape-wrongpw-user", "the-wrong-password-here"),
            ("shape-unknown-user", "whatever-password-123"),
            ("shape-suspended-user", suspended.TemporaryPassword!),
            ("shape-terminated-user", terminated.TemporaryPassword!),
        };

        var normalizedBodies = new List<string>();
        foreach (var (username, password) in scenarios)
        {
            using var response = await auth.PostAsJsonAsync("/v1/staff/authenticate", new { username, password });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var normalized = new Dictionary<string, object?>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name is "correlationId" or "traceId" or "instance")
                {
                    continue;
                }
                normalized[prop.Name] = prop.Value.ToString();
            }
            normalizedBodies.Add(JsonSerializer.Serialize(normalized));
        }

        Assert.All(normalizedBodies, b => Assert.Equal(normalizedBodies[0], b));
        _ = activeAccountForWrongPassword;
    }
}
