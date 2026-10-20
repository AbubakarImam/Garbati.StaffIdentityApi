namespace Garbati.StaffIdentityApi.Entities;

// DESIGN.md Section 2.2. Exactly these seven, in this order. Stored as a string column (Data/
// StaffIdentityDbContext.cs' HasConversion<string>()), not the enum's ordinal, so a future reordering of
// this list can never silently corrupt a value already written to the database.
public enum Role
{
    Teller,
    CustomerServiceOfficer,
    Supervisor,
    ComplianceOfficer,
    RiskOfficer,
    Auditor,
    Administrator,
}
