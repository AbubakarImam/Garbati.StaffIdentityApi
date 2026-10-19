# Garbati.StaffIdentityApi

GARBATI's staff/workforce identity service — who is logging into the bank's internal ops console, and what role do they hold. Built with ASP.NET Core 8, EF Core and PostgreSQL, matching the rest of the platform.

This is deliberately a separate identity domain from [Garbati.IdentityApi](https://github.com/AbubakarImam/Garbati.IdentityApi), which owns *customer* identity (KYC, mandates). Real core-banking platforms keep staff/workforce identity and customer identity (CIAM) structurally distinct — see `research/staff-ops-identity-patterns.md` in [garbati-platform](https://github.com/AbubakarImam/garbati-platform) for why, and `garbati-platform/TASKS.md` D-13 / `ARCHITECTURE.md` P-15 for the ratified decision.

The design, including every trade-off, is in [docs/DESIGN.md](docs/DESIGN.md) — start there for the *why*. See [`garbati-platform/ARCHITECTURE.md`](https://github.com/AbubakarImam/garbati-platform/blob/main/ARCHITECTURE.md) Section 3.1 for how this service fits the ops-console flow: **Ops console (browser) → OpsApi → StaffIdentityApi / IdentityApi / OrchestratorApi / LedgerApi**.

## Status

Design v1 (minimal) ratified, 19 October 2026. Not yet built — see `docs/DESIGN.md` Section 5 for what the first build task covers.

## This service's one caller

`OpsApi` (the ops console's backend-for-frontend, not yet built) is the only caller — never a browser directly, never another GARBATI service. `StaffIdentityApi` and `IdentityApi` never call each other.
