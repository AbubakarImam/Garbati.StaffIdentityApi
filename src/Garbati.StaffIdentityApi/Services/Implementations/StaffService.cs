using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Garbati.StaffIdentityApi.Auditing;
using Garbati.StaffIdentityApi.Authorization;
using Garbati.StaffIdentityApi.Contracts.Requests;
using Garbati.StaffIdentityApi.Contracts.Responses;
using Garbati.StaffIdentityApi.Data;
using Garbati.StaffIdentityApi.Entities;
using Garbati.StaffIdentityApi.Exceptions;
using Garbati.StaffIdentityApi.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Garbati.StaffIdentityApi.Services.Implementations;

public class StaffService(
    StaffIdentityDbContext dbContext,
    IStaffAuditLogger auditLogger,
    ICurrentClient currentClient,
    IPasswordHasher<StaffUser> passwordHasher) : IStaffService
{
    private const int MaxStaffIdGenerationAttempts = 5;

    // Exact constraint names from Migrations/*InitialCreate* (EFCore.NamingConventions' snake_case
    // naming of EF's default "IX_<Table>_<Columns>" index names) - verified against the generated
    // migration, not guessed, since a wrong name here would silently fall through to the "else" branch
    // below and misreport a real conflict as a StaffId collision.
    private const string IdempotencyKeyConstraintName = "ix_staff_users_created_by_client_id_idempotency_key";
    private const string NormalizedUsernameConstraintName = "ix_staff_users_normalized_username";

    private static string Normalize(string username) => username.Trim().ToUpperInvariant();

    // Fingerprint of the fields that define the staff account being created, so a reused idempotency key
    // with a different body is detected (same scheme as LedgerApi's AccountService/IdentityApi's
    // CustomerService/OrchestratorApi's AccountOpeningService).
    private static string HashRequest(CreateStaffRequest request) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new[] { request.ActingStaffId, request.FullName, request.Username, request.Role }))));

    private static StaffResponse ToResponse(StaffUser staff) => new(
        staff.StaffId,
        staff.FullName,
        staff.Username,
        staff.Role.ToString(),
        staff.Status.ToString(),
        staff.MustChangePassword,
        staff.SupervisorStaffId,
        staff.CreatedAt,
        staff.CreatedByStaffId,
        staff.UpdatedAt);

    public async Task<AuthenticateResponse?> AuthenticateAsync(AuthenticateRequest request, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(request.Username);
        var staff = await dbContext.StaffUsers.FirstOrDefaultAsync(s => s.NormalizedUsername == normalized, cancellationToken);

        // Fail closed on anything unexpected about the stored hash (e.g. the seeded Administrator's
        // deliberately-invalid placeholder hash, Migrations/*SeedInitialAdministrator*) rather than let a
        // FormatException escape as a bare 500 - ARCHITECTURE.md Section 1 principle 3.
        var verification = PasswordVerificationResult.Failed;
        if (staff is not null)
        {
            try
            {
                verification = passwordHasher.VerifyHashedPassword(staff, staff.PasswordHash, request.Password);
            }
            catch (FormatException)
            {
                verification = PasswordVerificationResult.Failed;
            }
        }

        // DESIGN.md Section 3, endpoint 1: unknown username, wrong password and Status != Active are all
        // the exact same outcome to the caller - computed here as one combined boolean so there is only
        // one success path below, not three near-identical ones that could drift apart.
        var succeeded = staff is not null && staff.Status == StaffStatus.Active && verification != PasswordVerificationResult.Failed;

        if (!succeeded)
        {
            // TargetStaffId is "whose account this is about" (DESIGN.md 2.5); for an unknown username
            // there is no real StaffId to put there, so the attempted username stands in for it - it is
            // also recorded in Details, per the design doc's own wording for this field.
            auditLogger.Record(new StaffAuditLog
            {
                Action = StaffAuditActions.AuthenticationFailed,
                TargetStaffId = staff?.StaffId ?? request.Username,
                ActingStaffId = null,
                Details = $"Attempted username: {request.Username}",
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        // PasswordHasher<TUser> upgrades its own algorithm/iteration count over time; a successful verify
        // against an older format silently re-hashes and saves on this same successful login (standard
        // PasswordHasher<TUser> behavior, DESIGN.md Section 4/task file - not extra scope).
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            staff!.PasswordHash = passwordHasher.HashPassword(staff, request.Password);
        }

        staff!.UpdatedAt = DateTimeOffset.UtcNow;
        auditLogger.Record(new StaffAuditLog
        {
            Action = StaffAuditActions.AuthenticationSucceeded,
            TargetStaffId = staff.StaffId,
            ActingStaffId = staff.StaffId,
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthenticateResponse(staff.StaffId, staff.FullName, staff.Role.ToString());
    }

    public async Task<CreateStaffResponse> CreateAsync(CreateStaffRequest request, CancellationToken cancellationToken = default)
    {
        var role = Enum.Parse<Role>(request.Role);
        var clientId = currentClient.ClientId;
        var requestHash = HashRequest(request);
        var normalizedUsername = Normalize(request.Username);

        for (var attempt = 1; attempt <= MaxStaffIdGenerationAttempts; attempt++)
        {
            var now = DateTimeOffset.UtcNow;
            var temporaryPassword = TemporaryCredentialGenerator.Generate();

            var staff = new StaffUser
            {
                StaffId = StaffIdGenerator.Generate(),
                FullName = request.FullName,
                Username = request.Username,
                NormalizedUsername = normalizedUsername,
                Role = role,
                Status = StaffStatus.Active,
                // DESIGN.md Section 2.1/3: an admin-created account always starts MustChangePassword.
                MustChangePassword = true,
                SupervisorStaffId = null,
                CreatedAt = now,
                CreatedByStaffId = request.ActingStaffId,
                UpdatedAt = now,
                IdempotencyKey = request.IdempotencyKey,
                CreatedByClientId = clientId,
                RequestHash = requestHash,
                PasswordHash = string.Empty,
            };
            staff.PasswordHash = passwordHasher.HashPassword(staff, temporaryPassword);

            await dbContext.StaffUsers.AddAsync(staff, cancellationToken);
            var audit = auditLogger.Record(new StaffAuditLog
            {
                Action = StaffAuditActions.StaffCreated,
                TargetStaffId = staff.StaffId,
                ActingStaffId = request.ActingStaffId,
                Details = $"Role={role}",
            });

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" } pgEx)
            {
                // Dependents before principals (EF navigation-fixup ordering - mirrors
                // OrchestratorApi's AccountOpeningService).
                dbContext.Entry(staff).State = EntityState.Detached;
                dbContext.Entry(audit).State = EntityState.Detached;

                if (pgEx.ConstraintName == IdempotencyKeyConstraintName)
                {
                    var existing = await dbContext.StaffUsers.AsNoTracking().FirstOrDefaultAsync(
                        s => s.CreatedByClientId == clientId && s.IdempotencyKey == request.IdempotencyKey, cancellationToken);

                    if (existing is null)
                        throw new InvalidOperationException("Error retrieving the result of the existing idempotency key.");

                    // Same key, different request: refuse rather than report the old staff account as
                    // this one's (409, ARCHITECTURE.md Section 5).
                    if (existing.RequestHash != requestHash)
                        throw new DuplicateIdempotencyKeyException(
                            $"Idempotency key '{request.IdempotencyKey}' was already used for a different request.");

                    // Same key, same request: a retry of an already-created account. The one-time
                    // temporary credential was only ever returned on the original call - task file: "the
                    // one-time credential is never returned on a second (idempotent-replay) response the
                    // same way it was on the first." The plaintext was never persisted, so there would be
                    // no way to return it again even if this wanted to.
                    return new CreateStaffResponse(ToResponse(existing), TemporaryPassword: null);
                }

                if (pgEx.ConstraintName == NormalizedUsernameConstraintName)
                {
                    throw new DuplicateUsernameException($"Username '{request.Username}' is already in use.");
                }

                // Otherwise the collision was on StaffId: loop around and try another one.
                continue;
            }

            return new CreateStaffResponse(ToResponse(staff), temporaryPassword);
        }

        throw new InvalidOperationException($"Could not allocate a unique StaffId after {MaxStaffIdGenerationAttempts} attempts.");
    }

    public async Task<StaffResponse?> GetAsync(string staffId, CancellationToken cancellationToken = default)
    {
        var staff = await dbContext.StaffUsers.AsNoTracking().FirstOrDefaultAsync(s => s.StaffId == staffId, cancellationToken);
        return staff is null ? null : ToResponse(staff);
    }

    public async Task<StaffListResponse> ListAsync(string? supervisorStaffId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = dbContext.StaffUsers.AsNoTracking().AsQueryable();
        if (supervisorStaffId is not null)
        {
            query = query.Where(s => s.SupervisorStaffId == supervisorStaffId);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(s => s.CreatedAt).ThenBy(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new StaffListResponse(items.Select(ToResponse).ToList(), page, pageSize, totalCount);
    }

    public async Task<StaffResponse> ChangeRoleAsync(string staffId, ChangeRoleRequest request, CancellationToken cancellationToken = default)
    {
        var staff = await GetTrackedAsync(staffId, cancellationToken);
        var newRole = Enum.Parse<Role>(request.Role);
        var oldRole = staff.Role;

        // DESIGN.md Section 3, endpoint 5: deliberately does NOT touch SupervisorStaffId, even when the
        // new role is no longer Teller/CustomerServiceOfficer - PATCH .../supervisor is a separate,
        // independently-audited call.
        staff.Role = newRole;
        staff.UpdatedAt = DateTimeOffset.UtcNow;

        auditLogger.Record(new StaffAuditLog
        {
            Action = StaffAuditActions.RoleChanged,
            TargetStaffId = staff.StaffId,
            ActingStaffId = request.ActingStaffId,
            Details = $"{oldRole} -> {newRole}",
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(staff);
    }

    public async Task<StaffResponse> ChangeStatusAsync(string staffId, ChangeStatusRequest request, CancellationToken cancellationToken = default)
    {
        var staff = await GetTrackedAsync(staffId, cancellationToken);
        var newStatus = Enum.Parse<StaffStatus>(request.Status);
        var oldStatus = staff.Status;

        // Repeating the current status is a no-op (same "idempotent repeat" convention as this
        // platform's other create/suspend/revoke-style endpoints) - no audit row, no transition check.
        if (oldStatus == newStatus)
        {
            return ToResponse(staff);
        }

        // DESIGN.md Section 3, endpoint 6: Terminated is a true dead end. This is the one and only place
        // in this service that can move a staff member OUT of Terminated, and it refuses every time.
        if (oldStatus == StaffStatus.Terminated)
        {
            throw new InvalidStatusTransitionException(
                $"Staff member '{staffId}' is Terminated; no transition out of Terminated is allowed.");
        }

        staff.Status = newStatus;
        staff.UpdatedAt = DateTimeOffset.UtcNow;

        auditLogger.Record(new StaffAuditLog
        {
            Action = StaffAuditActions.StatusChanged,
            TargetStaffId = staff.StaffId,
            ActingStaffId = request.ActingStaffId,
            Details = $"{oldStatus} -> {newStatus}",
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(staff);
    }

    public async Task<StaffResponse> ChangeSupervisorAsync(string staffId, ChangeSupervisorRequest request, CancellationToken cancellationToken = default)
    {
        var staff = await GetTrackedAsync(staffId, cancellationToken);
        var oldSupervisorStaffId = staff.SupervisorStaffId;

        if (request.SupervisorStaffId is not null)
        {
            // DESIGN.md Section 2.4, decision #4: self-reference is forbidden, checked before the
            // database lookup below (a self-reference is always wrong regardless of what else is true).
            if (request.SupervisorStaffId == staffId)
            {
                throw new InvalidSupervisorAssignmentException("A staff member cannot be their own supervisor.");
            }

            var supervisor = await dbContext.StaffUsers.AsNoTracking()
                .FirstOrDefaultAsync(s => s.StaffId == request.SupervisorStaffId, cancellationToken);

            // Must reference an existing, Active StaffUser whose Role is SPECIFICALLY Supervisor - never
            // Administrator, never any other role (DESIGN.md 2.4: "An Administrator is a different
            // functional role ... deliberately not accepted here").
            if (supervisor is null || supervisor.Status != StaffStatus.Active || supervisor.Role != Role.Supervisor)
            {
                throw new InvalidSupervisorAssignmentException(
                    $"'{request.SupervisorStaffId}' does not reference an existing, Active, Supervisor-role staff member.");
            }
        }

        staff.SupervisorStaffId = request.SupervisorStaffId;
        staff.UpdatedAt = DateTimeOffset.UtcNow;

        auditLogger.Record(new StaffAuditLog
        {
            Action = StaffAuditActions.SupervisorChanged,
            TargetStaffId = staff.StaffId,
            ActingStaffId = request.ActingStaffId,
            Details = $"{oldSupervisorStaffId ?? "(none)"} -> {request.SupervisorStaffId ?? "(none)"}",
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(staff);
    }

    public async Task<ChangePasswordResponse> AdminResetPasswordAsync(string staffId, ChangeStaffPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var staff = await GetTrackedAsync(staffId, cancellationToken);

        // Judgment call (see the PR description): the admin branch never accepts a plaintext password
        // over the wire, the same convention POST /v1/staff already uses - a fresh one-time temporary
        // credential is always generated here instead.
        var temporaryPassword = TemporaryCredentialGenerator.Generate();
        staff.PasswordHash = passwordHasher.HashPassword(staff, temporaryPassword);
        staff.MustChangePassword = true;
        staff.UpdatedAt = DateTimeOffset.UtcNow;

        auditLogger.Record(new StaffAuditLog
        {
            Action = StaffAuditActions.PasswordReset,
            TargetStaffId = staff.StaffId,
            ActingStaffId = request.ActingStaffId,
            Details = "Admin-driven reset.",
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ChangePasswordResponse(staff.StaffId, temporaryPassword);
    }

    public async Task<ChangePasswordResponse> SelfServiceChangePasswordAsync(string staffId, ChangeStaffPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var staff = await GetTrackedAsync(staffId, cancellationToken);

        var verification = PasswordVerificationResult.Failed;
        try
        {
            verification = passwordHasher.VerifyHashedPassword(staff, staff.PasswordHash, request.CurrentPassword!);
        }
        catch (FormatException)
        {
            verification = PasswordVerificationResult.Failed;
        }

        if (verification == PasswordVerificationResult.Failed)
        {
            // Clear failure (400), not a silent no-op - task file's own requirement.
            throw new IncorrectCurrentPasswordException("Current password is incorrect.");
        }

        staff.PasswordHash = passwordHasher.HashPassword(staff, request.NewPassword!);
        staff.MustChangePassword = false;
        staff.UpdatedAt = DateTimeOffset.UtcNow;

        auditLogger.Record(new StaffAuditLog
        {
            Action = StaffAuditActions.PasswordReset,
            TargetStaffId = staff.StaffId,
            ActingStaffId = request.ActingStaffId,
            Details = "Self-service change.",
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ChangePasswordResponse(staff.StaffId, TemporaryPassword: null);
    }

    private async Task<StaffUser> GetTrackedAsync(string staffId, CancellationToken cancellationToken) =>
        await dbContext.StaffUsers.FirstOrDefaultAsync(s => s.StaffId == staffId, cancellationToken)
            ?? throw new StaffNotFoundException($"Staff member '{staffId}' was not found.");
}
