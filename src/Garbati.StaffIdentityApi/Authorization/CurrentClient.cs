namespace Garbati.StaffIdentityApi.Authorization;

// The API client making the current request (the client id from its API key), used only to scope POST
// /v1/staff's idempotency key (ARCHITECTURE.md Section 5: "unique per calling client"). Null outside an
// authenticated request, e.g. in service-level tests.
public interface ICurrentClient
{
    string? ClientId { get; }
}

public class HttpCurrentClient(IHttpContextAccessor httpContextAccessor) : ICurrentClient
{
    public string? ClientId => httpContextAccessor.HttpContext?.User.Identity?.Name;
}
