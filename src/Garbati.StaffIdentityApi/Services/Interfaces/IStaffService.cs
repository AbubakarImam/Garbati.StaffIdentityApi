using Garbati.StaffIdentityApi.Contracts.Requests;
using Garbati.StaffIdentityApi.Contracts.Responses;

namespace Garbati.StaffIdentityApi.Services.Interfaces;

public interface IStaffService
{
    // Null means "authentication failed" for any reason at all (unknown username, wrong password,
    // Status != Active) - the controller turns null into a single, generic 401 either way
    // (DESIGN.md Section 3, endpoint 1: no distinguishing signal).
    Task<AuthenticateResponse?> AuthenticateAsync(AuthenticateRequest request, CancellationToken cancellationToken = default);

    Task<CreateStaffResponse> CreateAsync(CreateStaffRequest request, CancellationToken cancellationToken = default);

    Task<StaffResponse?> GetAsync(string staffId, CancellationToken cancellationToken = default);

    Task<StaffListResponse> ListAsync(string? supervisorStaffId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<StaffResponse> ChangeRoleAsync(string staffId, ChangeRoleRequest request, CancellationToken cancellationToken = default);

    Task<StaffResponse> ChangeStatusAsync(string staffId, ChangeStatusRequest request, CancellationToken cancellationToken = default);

    Task<StaffResponse> ChangeSupervisorAsync(string staffId, ChangeSupervisorRequest request, CancellationToken cancellationToken = default);

    Task<ChangePasswordResponse> AdminResetPasswordAsync(string staffId, ChangeStaffPasswordRequest request, CancellationToken cancellationToken = default);

    Task<ChangePasswordResponse> SelfServiceChangePasswordAsync(string staffId, ChangeStaffPasswordRequest request, CancellationToken cancellationToken = default);
}
