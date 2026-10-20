using System.Reflection;
using Garbati.StaffIdentityApi.Authorization;
using Garbati.StaffIdentityApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Garbati.StaffIdentityApi.Tests.Authorization;

// Guards new endpoints: every controller action must name exactly one known scope policy. The fallback
// policy would still demand a key, but any key at all is not the intended rule. Same test as LedgerApi's/
// IdentityApi's/OrchestratorApi's ControllerAuthorizationTests.
public class ControllerAuthorizationTests
{
    // DESIGN.md Section 3, endpoint 8: StaffController.ChangePassword is a deliberate, narrow exception to
    // the "exactly one static scope policy" rule below - POST /v1/staff/{staffId}/password serves two
    // different real-world actions (admin-driven reset, self-service change) that need two different,
    // mutually exclusive scopes depending on which credential the caller presents, not on the route.
    // [Authorize(Policy=...)] is evaluated before the action even runs, so a single static attribute
    // cannot express that choice - the action checks the right scope explicitly instead (see its own
    // comment) and still uses [Authorize] (no policy) so it is never reachable without SOME valid API key.
    // Exercised by Controllers/PasswordTests.cs instead of this structural test.
    private static readonly HashSet<(Type DeclaringType, string Name)> BodyDependentScopeExceptions =
        [(typeof(StaffController), nameof(StaffController.ChangePassword))];

    [Fact]
    public void EveryControllerAction_RequiresAKnownScopePolicy()
    {
        var actions = typeof(Program).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(ControllerBase)) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName)
            .Where(m => !BodyDependentScopeExceptions.Contains((m.DeclaringType!, m.Name)))
            .ToList();

        var violations = actions
            .Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null
                || m.GetCustomAttribute<AuthorizeAttribute>()?.Policy is not { } policy
                || !StaffIdentityScopes.All.Contains(policy))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .ToList();

        Assert.True(actions.Count >= 7, $"Expected at least 7 controller actions, found {actions.Count}.");
        Assert.Empty(violations);
    }

    // The exempted action still needs SOME guard - this proves it carries a plain [Authorize] (so the
    // fallback-anonymous risk the test above exists to catch doesn't quietly reappear here instead).
    [Fact]
    public void ChangePassword_IsNotAnonymous_EvenThoughItIsExemptFromTheScopePolicyCheck()
    {
        var method = typeof(StaffController).GetMethod(nameof(StaffController.ChangePassword))!;

        Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(method.GetCustomAttribute<AuthorizeAttribute>());
    }
}
