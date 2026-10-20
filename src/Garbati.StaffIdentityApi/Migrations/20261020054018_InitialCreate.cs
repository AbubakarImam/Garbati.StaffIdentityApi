using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Garbati.StaffIdentityApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staff_audit_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    action = table.Column<string>(type: "text", nullable: false),
                    target_staff_id = table.Column<string>(type: "text", nullable: false),
                    acting_staff_id = table.Column<string>(type: "text", nullable: true),
                    details = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "staff_users",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    staff_id = table.Column<string>(type: "text", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    username = table.Column<string>(type: "text", nullable: false),
                    normalized_username = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    must_change_password = table.Column<bool>(type: "boolean", nullable: false),
                    supervisor_staff_id = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_staff_id = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    idempotency_key = table.Column<string>(type: "text", nullable: true),
                    created_by_client_id = table.Column<string>(type: "text", nullable: true),
                    request_hash = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_users", x => x.id);
                    table.UniqueConstraint("ak_staff_users_staff_id", x => x.staff_id);
                    table.CheckConstraint("ck_staff_users_role", "\"role\" IN ('Teller', 'CustomerServiceOfficer', 'Supervisor', 'ComplianceOfficer', 'RiskOfficer', 'Auditor', 'Administrator')");
                    table.CheckConstraint("ck_staff_users_status", "\"status\" IN ('Active', 'Suspended', 'Terminated')");
                    table.ForeignKey(
                        name: "fk_staff_users_staff_users_supervisor_staff_id",
                        column: x => x.supervisor_staff_id,
                        principalTable: "staff_users",
                        principalColumn: "staff_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_staff_audit_logs_target_staff_id",
                table: "staff_audit_logs",
                column: "target_staff_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_users_created_by_client_id_idempotency_key",
                table: "staff_users",
                columns: new[] { "created_by_client_id", "idempotency_key" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_staff_users_normalized_username",
                table: "staff_users",
                column: "normalized_username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_staff_users_staff_id",
                table: "staff_users",
                column: "staff_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_staff_users_supervisor_staff_id",
                table: "staff_users",
                column: "supervisor_staff_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_audit_logs");

            migrationBuilder.DropTable(
                name: "staff_users");
        }
    }
}
