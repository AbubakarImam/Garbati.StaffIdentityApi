using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garbati.StaffIdentityApi.Migrations
{
    /// <summary>
    /// DESIGN.md Section 2.5 decision #2 / task file: the very first Administrator account, seeded
    /// because POST /v1/staff itself requires an already-authenticated Administrator caller, and the
    /// first one has to come from somewhere.
    ///
    /// Judgment call (full reasoning in the PR description): this seeds a DELIBERATELY INVALID
    /// placeholder password hash - 32 bytes of 0xFF, base64-encoded below - not a real password hash of
    /// any kind, generated or otherwise. PasswordHasher&lt;TUser&gt;.VerifyHashedPassword reads the first
    /// decoded byte as a format marker (0x00 or 0x01 are its two real formats); 0xFF matches neither, so
    /// it safely returns Failed for every possible password, forever, without throwing. No password,
    /// generated or chosen, is ever baked into this committed file.
    ///
    /// The seeded account is unusable until an operator calls the already-specified
    /// POST /v1/staff/{staffId}/password with a staffidentity.admin-scoped key (passing this row's own
    /// StaffId as both the route parameter and ActingStaffId) - that is this service's ordinary
    /// admin-driven password reset, DESIGN.md Section 3 endpoint 8, reused as the one-time bootstrap step
    /// rather than inventing a second, parallel mechanism DESIGN.md's endpoint table does not list. That
    /// reset generates its own fresh one-time credential server-side and returns it once, exactly like
    /// every other admin-driven reset - there is nothing special-cased about the seeded row once it
    /// exists.
    /// </summary>
    public partial class SeedInitialAdministrator : Migration
    {
        private const string StaffId = "STF-0000000001";

        // 32 bytes of 0xFF, base64-encoded - see the class remarks above. Computed, not copy-pasted from
        // anywhere a real credential could have touched.
        private const string PlaceholderPasswordHash = "//////////////////////////////////////////8=";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                INSERT INTO staff_users
                    (staff_id, full_name, username, normalized_username, password_hash, role, status,
                     must_change_password, supervisor_staff_id, created_at, created_by_staff_id, updated_at,
                     idempotency_key, created_by_client_id, request_hash)
                VALUES
                    ('{StaffId}', 'Initial Administrator', 'admin', 'ADMIN', '{PlaceholderPasswordHash}',
                     'Administrator', 'Active', true, NULL, now(), NULL, now(), NULL, NULL, NULL)
                ON CONFLICT (staff_id) DO NOTHING;
                """);

            // DESIGN.md 2.5: ActingStaffId is nullable only here and for AuthenticationFailed - nobody
            // "acted" to create the very first Administrator, the deployment itself did.
            migrationBuilder.Sql($"""
                INSERT INTO staff_audit_logs (action, target_staff_id, acting_staff_id, details, created_at)
                VALUES ('StaffCreated', '{StaffId}', NULL, 'Seeded initial Administrator account at deployment.', now());
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                DELETE FROM staff_audit_logs WHERE action = 'StaffCreated' AND target_staff_id = '{StaffId}' AND acting_staff_id IS NULL;
                """);
            migrationBuilder.Sql($"DELETE FROM staff_users WHERE staff_id = '{StaffId}';");
        }
    }
}
