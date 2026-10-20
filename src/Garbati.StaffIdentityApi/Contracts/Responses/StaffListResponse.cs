namespace Garbati.StaffIdentityApi.Contracts.Responses;

// DESIGN.md Section 3, endpoint 4: "Paged list/search."
public record StaffListResponse(IReadOnlyList<StaffResponse> Items, int Page, int PageSize, int TotalCount);
