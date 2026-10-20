using System.Net.Http.Json;
using Garbati.StaffIdentityApi.Contracts.Responses;

namespace Garbati.StaffIdentityApi.Tests.Fixtures;

// Shared request-building helpers so every test file doesn't re-derive the same JSON shapes.
public static class StaffApiHelpers
{
    public const string DefaultActingStaffId = "STF-TESTACTOR";

    public static async Task<CreateStaffResponse> CreateStaffAsync(
        HttpClient adminClient,
        string role = "Teller",
        string? username = null,
        string? fullName = null,
        string actingStaffId = DefaultActingStaffId)
    {
        username ??= $"user-{Guid.NewGuid():N}"[..20];
        fullName ??= "Test Staff Member";

        var request = new
        {
            idempotencyKey = $"test-create-{Guid.NewGuid():N}",
            actingStaffId,
            fullName,
            username,
            role,
        };

        var response = await adminClient.PostAsJsonAsync("/v1/staff", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateStaffResponse>())!;
    }

    public static async Task SetStatusAsync(HttpClient adminClient, string staffId, string status, string actingStaffId = DefaultActingStaffId)
    {
        var response = await adminClient.PatchAsJsonAsync($"/v1/staff/{staffId}/status", new { actingStaffId, status });
        response.EnsureSuccessStatusCode();
    }
}
