namespace Garbati.StaffIdentityApi.Entities;

// DESIGN.md Section 2.1: "Terminated is permanent - no path back to Active, mirroring IdentityApi's
// Closed/Deceased being terminal. Suspended can be reactivated to Active." Stored as a string column,
// same reasoning as Role.
public enum StaffStatus
{
    Active,
    Suspended,
    Terminated,
}
