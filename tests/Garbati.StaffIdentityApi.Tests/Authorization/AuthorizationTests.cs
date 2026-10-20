using System.Net;
using System.Text;
using System.Text.Json;
using Garbati.StaffIdentityApi.Authorization;
using Garbati.StaffIdentityApi.Middleware;
using Garbati.StaffIdentityApi.Tests.Fixtures;

namespace Garbati.StaffIdentityApi.Tests.Authorization;

// Task file: "API-key auth and scopes: no key -> 401; wrong scope for the endpoint -> 403". Same test
// shape as LedgerApi's/IdentityApi's/OrchestratorApi's AuthorizationTests. POST /v1/staff/{staffId}/password
// is deliberately excluded - it accepts EITHER scope (DESIGN.md Section 3, endpoint 8), so it has its own
// dedicated authorization tests in Controllers/PasswordTests.cs instead of fitting this one-scope-per-route matrix.
[Collection("Api")]
public class AuthorizationTests(ApiFixture fixture)
{
    private static string AuthenticateBody() => """{"username":"authz-test-user","password":"whatever-password-12"}""";
    private static string CreateStaffBody() => """
        {"idempotencyKey":"authz-test-create","actingStaffId":"STF-TESTACTOR","fullName":"Authz Test",
         "username":"authz-test-user","role":"Teller"}
        """;
    private static string ChangeRoleBody() => """{"actingStaffId":"STF-TESTACTOR","role":"Teller"}""";
    private static string ChangeStatusBody() => """{"actingStaffId":"STF-TESTACTOR","status":"Active"}""";
    private static string ChangeSupervisorBody() => """{"actingStaffId":"STF-TESTACTOR","supervisorStaffId":null}""";

    public static TheoryData<string, string, string?, string> Endpoints => new()
    {
        { "POST", "/v1/staff/authenticate", AuthenticateBody(), StaffIdentityScopes.Authenticate },
        { "POST", "/v1/staff", CreateStaffBody(), StaffIdentityScopes.Admin },
        { "GET", "/v1/staff/STF-DOESNOTEXIST", null, StaffIdentityScopes.Admin },
        { "GET", "/v1/staff", null, StaffIdentityScopes.Admin },
        { "PATCH", "/v1/staff/STF-DOESNOTEXIST/role", ChangeRoleBody(), StaffIdentityScopes.Admin },
        { "PATCH", "/v1/staff/STF-DOESNOTEXIST/status", ChangeStatusBody(), StaffIdentityScopes.Admin },
        { "PATCH", "/v1/staff/STF-DOESNOTEXIST/supervisor", ChangeSupervisorBody(), StaffIdentityScopes.Admin },
    };

    private static HttpRequestMessage Request(string method, string path, string? body)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }
        return request;
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Returns401_WithProblemDetails_WhenNoApiKeyIsSent(string method, string path, string? body, string scope)
    {
        _ = scope;
        using var client = fixture.CreateClient(clientId: null);

        using var response = await client.SendAsync(Request(method, path, body));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ApiKeyAuthenticationHandler.SchemeName, response.Headers.WwwAuthenticate.ToString());
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var correlationId = response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single();
        Assert.Equal(correlationId, problem.RootElement.GetProperty("correlationId").GetString());
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Returns403_WhenTheKeyLacksTheRequiredScope(string method, string path, string? body, string scope)
    {
        using var client = fixture.CreateClient(ApiFixture.AllButClient(scope));

        using var response = await client.SendAsync(Request(method, path, body));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task ReachesTheEndpoint_WhenTheKeyHoldsOnlyTheRequiredScope(string method, string path, string? body, string scope)
    {
        using var client = fixture.CreateClient(ApiFixture.OnlyClient(scope));

        using var response = await client.SendAsync(Request(method, path, body));

        // Past authorization the controller answers normally (200/401/404 depending on the data) - never 403.
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Returns401_ForAnUnknownApiKey()
    {
        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthenticationHandler.HeaderName, "idk_not_a_real_key");

        using var response = await client.GetAsync("/v1/staff/STF-DOESNOTEXIST");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
