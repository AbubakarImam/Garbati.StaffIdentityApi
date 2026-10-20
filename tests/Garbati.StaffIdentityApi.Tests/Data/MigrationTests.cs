using Garbati.StaffIdentityApi.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Garbati.StaffIdentityApi.Tests.Data;

// Confirms the migrated database schema actually matches Entities/StaffUser.cs and Entities/
// StaffAuditLog.cs - not just that EF's in-memory model does. Mirrors OrchestratorApi's/LedgerApi's own
// MigrationTests style.
[Collection("Api")]
public class MigrationTests(ApiFixture fixture)
{
    [Fact]
    public async Task AppliedMigrations_IncludeInitialCreateAndSeedInitialAdministrator()
    {
        await using var ctx = fixture.CreateContext();

        var applied = await ctx.Database.GetAppliedMigrationsAsync();

        Assert.Contains(applied, m => m.EndsWith("InitialCreate"));
        Assert.Contains(applied, m => m.EndsWith("SeedInitialAdministrator"));
    }

    [Fact]
    public async Task StaffUsersTable_ExistsWithItsExpectedColumns()
    {
        await using var ctx = fixture.CreateContext();

        await AssertColumnsAsync(ctx, "staff_users",
            "id", "staff_id", "full_name", "username", "normalized_username", "password_hash",
            "role", "status", "must_change_password", "supervisor_staff_id",
            "created_at", "created_by_staff_id", "updated_at",
            "idempotency_key", "created_by_client_id", "request_hash");
    }

    [Fact]
    public async Task StaffAuditLogsTable_ExistsWithItsExpectedColumns()
    {
        await using var ctx = fixture.CreateContext();

        await AssertColumnsAsync(ctx, "staff_audit_logs",
            "id", "action", "target_staff_id", "acting_staff_id", "details", "created_at");
    }

    private static async Task AssertColumnsAsync(Microsoft.EntityFrameworkCore.DbContext ctx, string table, params string[] expectedColumns)
    {
        var actual = new List<string>();
        var connection = ctx.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT column_name FROM information_schema.columns WHERE table_name = @table";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "table";
        parameter.Value = table;
        command.Parameters.Add(parameter);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            actual.Add(reader.GetString(0));
        }

        foreach (var expected in expectedColumns)
        {
            Assert.Contains(expected, actual);
        }
    }
}
