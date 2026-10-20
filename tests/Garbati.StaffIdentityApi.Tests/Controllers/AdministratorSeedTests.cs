using System.Net;
using System.Net.Http.Json;
using Garbati.StaffIdentityApi.Authorization;
using Garbati.StaffIdentityApi.Contracts.Responses;
using Garbati.StaffIdentityApi.Entities;
using Garbati.StaffIdentityApi.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Garbati.StaffIdentityApi.Tests.Controllers;

// DESIGN.md Section 2.5 decision #2 / task file: "The Administrator seed exists exactly once after
// migrations run, with MustChangePassword = true and whatever password-bootstrap mechanism you chose
// actually working end-to-end (not just that a row exists)." This class proves both halves.
[Collection("Api")]
public class AdministratorSeedTests(ApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ExactlyOneAdministrator_IsSeededByMigrations_WithMustChangePasswordTrue()
    {
        await using var ctx = fixture.CreateContext();

        var administrators = await ctx.StaffUsers.Where(s => s.Role == Role.Administrator).ToListAsync();

        var seeded = Assert.Single(administrators);
        Assert.Equal(ApiFixture.SeededAdministratorStaffId, seeded.StaffId);
        Assert.True(seeded.MustChangePassword);
        Assert.Equal(StaffStatus.Active, seeded.Status);
        Assert.Null(seeded.CreatedByStaffId);
    }

    [Fact]
    public async Task SeededAdministrator_CannotAuthenticate_WithAnyPasswordUntilReset()
    {
        using var client = fixture.CreateClient(ApiFixture.OnlyClient(StaffIdentityScopes.Authenticate));

        using var response = await client.PostAsJsonAsync("/v1/staff/authenticate",
            new { username = "admin", password = "literally-anything-12345" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Proves the chosen bootstrap mechanism (PR description: reuse the already-specified admin-driven
    // password reset, DESIGN.md Section 3 endpoint 8, instead of inventing a new one) actually works
    // end-to-end: an operator calls it once against the seeded row, and the account becomes usable.
    [Fact]
    public async Task AdminResetOnTheSeededAdministrator_MakesTheAccountUsable_EndToEnd()
    {
        using var adminClient = fixture.CreateClient(ApiFixture.OnlyClient(StaffIdentityScopes.Admin));
        using var authClient = fixture.CreateClient(ApiFixture.OnlyClient(StaffIdentityScopes.Authenticate));

        using var resetResponse = await adminClient.PostAsJsonAsync(
            $"/v1/staff/{ApiFixture.SeededAdministratorStaffId}/password",
            new { actingStaffId = ApiFixture.SeededAdministratorStaffId });
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        var reset = await resetResponse.Content.ReadFromJsonAsync<ChangePasswordResponse>();
        Assert.NotNull(reset!.TemporaryPassword);

        using var authResponse = await authClient.PostAsJsonAsync("/v1/staff/authenticate",
            new { username = "admin", password = reset.TemporaryPassword });

        Assert.Equal(HttpStatusCode.OK, authResponse.StatusCode);
        var authenticated = await authResponse.Content.ReadFromJsonAsync<AuthenticateResponse>();
        Assert.Equal(ApiFixture.SeededAdministratorStaffId, authenticated!.StaffId);
        Assert.Equal(nameof(Role.Administrator), authenticated.Role);

        await using var ctx = fixture.CreateContext();
        var seeded = await ctx.StaffUsers.AsNoTracking().FirstAsync(s => s.StaffId == ApiFixture.SeededAdministratorStaffId);
        // The admin branch always sets MustChangePassword - the operator is still expected to hand this
        // one-time credential to the real administrator, who changes it again on first real use.
        Assert.True(seeded.MustChangePassword);
    }
}
