using Garbati.StaffIdentityApi.Authorization;
using Garbati.StaffIdentityApi.Contracts.Requests;
using Garbati.StaffIdentityApi.Contracts.Responses;
using Garbati.StaffIdentityApi.Services.Interfaces;
using Garbati.StaffIdentityApi.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Garbati.StaffIdentityApi.Controllers;

// DESIGN.md Section 3. Route literals (not [controller]) match the versioned paths the design doc spells
// out exactly (ARCHITECTURE.md Section 5: "/v1/ in the path").
[ApiController]
[Route("v1/staff")]
public class StaffController(
    IStaffService staffService,
    AuthenticateRequestValidator authenticateValidator,
    CreateStaffRequestValidator createStaffValidator,
    ChangeRoleRequestValidator changeRoleValidator,
    ChangeStatusRequestValidator changeStatusValidator,
    ChangeSupervisorRequestValidator changeSupervisorValidator,
    ChangeStaffPasswordRequestValidator changePasswordValidator) : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    [Authorize(Policy = StaffIdentityScopes.Authenticate)]
    [HttpPost("authenticate")]
    public async Task<ActionResult<AuthenticateResponse>> Authenticate(
        [FromBody] AuthenticateRequest request, CancellationToken cancellationToken)
    {
        var errors = authenticateValidator.Validate(request);
        if (errors.Count > 0)
        {
            return BadRequest(errors);
        }

        var response = await staffService.AuthenticateAsync(request, cancellationToken);

        // DESIGN.md Section 3, endpoint 1: wrong username, wrong password and Status != Active are all
        // the exact same 401, with no body distinguishing them - the handler below is reached only
        // through ApiKeyAuthenticationHandler.HandleChallengeAsync's shape, which already uses this same
        // ProblemDetails format, so there is no second, different 401 shape anywhere in this service.
        return response is null ? Unauthorized() : Ok(response);
    }

    [Authorize(Policy = StaffIdentityScopes.Admin)]
    [HttpPost]
    public async Task<ActionResult<CreateStaffResponse>> CreateStaff(
        [FromBody] CreateStaffRequest request, CancellationToken cancellationToken)
    {
        var errors = createStaffValidator.Validate(request);
        if (errors.Count > 0)
        {
            return BadRequest(errors);
        }

        var response = await staffService.CreateAsync(request, cancellationToken);
        return Ok(response);
    }

    [Authorize(Policy = StaffIdentityScopes.Admin)]
    [HttpGet("{staffId}")]
    public async Task<ActionResult<StaffResponse>> GetStaff(string staffId, CancellationToken cancellationToken)
    {
        var response = await staffService.GetAsync(staffId, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [Authorize(Policy = StaffIdentityScopes.Admin)]
    [HttpGet]
    public async Task<ActionResult<StaffListResponse>> ListStaff(
        [FromQuery] string? supervisorStaffId, [FromQuery] int page = 1, [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            return BadRequest(new[] { "page must be at least 1." });
        }
        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            return BadRequest(new[] { $"pageSize must be between 1 and {MaxPageSize}." });
        }

        var response = await staffService.ListAsync(supervisorStaffId, page, pageSize, cancellationToken);
        return Ok(response);
    }

    [Authorize(Policy = StaffIdentityScopes.Admin)]
    [HttpPatch("{staffId}/role")]
    public async Task<ActionResult<StaffResponse>> ChangeRole(
        string staffId, [FromBody] ChangeRoleRequest request, CancellationToken cancellationToken)
    {
        var errors = changeRoleValidator.Validate(request);
        if (errors.Count > 0)
        {
            return BadRequest(errors);
        }

        var response = await staffService.ChangeRoleAsync(staffId, request, cancellationToken);
        return Ok(response);
    }

    [Authorize(Policy = StaffIdentityScopes.Admin)]
    [HttpPatch("{staffId}/status")]
    public async Task<ActionResult<StaffResponse>> ChangeStatus(
        string staffId, [FromBody] ChangeStatusRequest request, CancellationToken cancellationToken)
    {
        var errors = changeStatusValidator.Validate(request);
        if (errors.Count > 0)
        {
            return BadRequest(errors);
        }

        var response = await staffService.ChangeStatusAsync(staffId, request, cancellationToken);
        return Ok(response);
    }

    [Authorize(Policy = StaffIdentityScopes.Admin)]
    [HttpPatch("{staffId}/supervisor")]
    public async Task<ActionResult<StaffResponse>> ChangeSupervisor(
        string staffId, [FromBody] ChangeSupervisorRequest request, CancellationToken cancellationToken)
    {
        var errors = changeSupervisorValidator.Validate(request);
        if (errors.Count > 0)
        {
            return BadRequest(errors);
        }

        var response = await staffService.ChangeSupervisorAsync(staffId, request, cancellationToken);
        return Ok(response);
    }

    // DESIGN.md Section 3, endpoint 8: one route, two behaviors distinguished by which scope claim the
    // caller's own credential holds - mirrors Garbati.IdentityApi's AuthorizationController.
    // RequestAuthorization, which branches on caller scope for the same structural reason: ASP.NET Core
    // evaluates [Authorize(Policy=...)] before the action even runs, so a single static attribute cannot
    // pick between two scopes - the explicit User.HasClaim checks below do that instead, after
    // confirming the caller holds SOME valid key ([Authorize], no policy). This produces the exact same
    // 403 ProblemDetails shape as a failed [Authorize] (ControllerBase.Forbid() triggers
    // ApiKeyAuthenticationHandler.HandleForbiddenAsync), so no new error shape exists. Exercised by
    // Controllers/PasswordTests.cs instead of the structural ControllerAuthorizationTests check (see that
    // test file's own exception list).
    [Authorize]
    [HttpPost("{staffId}/password")]
    public async Task<ActionResult<ChangePasswordResponse>> ChangePassword(
        string staffId, [FromBody] ChangeStaffPasswordRequest request, CancellationToken cancellationToken)
    {
        var isAdmin = User.HasClaim(StaffIdentityScopes.ClaimType, StaffIdentityScopes.Admin);
        var isSelfService = User.HasClaim(StaffIdentityScopes.ClaimType, StaffIdentityScopes.Authenticate);

        if (isAdmin)
        {
            var errors = changePasswordValidator.ValidateAdmin(request);
            if (errors.Count > 0)
            {
                return BadRequest(errors);
            }

            var response = await staffService.AdminResetPasswordAsync(staffId, request, cancellationToken);
            return Ok(response);
        }

        if (isSelfService)
        {
            var errors = changePasswordValidator.ValidateSelfService(request);
            if (errors.Count > 0)
            {
                return BadRequest(errors);
            }

            var response = await staffService.SelfServiceChangePasswordAsync(staffId, request, cancellationToken);
            return Ok(response);
        }

        // Authenticated (some valid key - [Authorize] already enforced that) but holding neither of this
        // endpoint's two accepted scopes.
        return Forbid();
    }
}
