# Garbati.StaffIdentityApi — Design Document (v1, minimal)

**Status:** Design v1 (minimal) — ratified, not yet built.
**Last updated:** 19 October 2026
**Service name:** `StaffIdentityApi` (`Garbati.StaffIdentityApi`)
**Platform:** Garbati — see [`garbati-platform/ARCHITECTURE.md`](https://github.com/AbubakarImam/garbati-platform/blob/main/ARCHITECTURE.md) Section 2 (service map), Section 3.1 (ops-console dependency chain) and P-15 for how this service fits the rest of the platform. This document never repeats that file, only this service's detail.
**Decision history:** `garbati-platform/TASKS.md` D-13 (staff identity gets its own service, 19 Oct 2026), T-026 (`research/staff-ops-identity-patterns.md` — the research this design applies).

## 1. Purpose and scope

StaffIdentityApi answers exactly one question, for exactly one caller: **"who is this bank employee, and what role do they hold?"** It is the workforce-IAM half of GARBATI's identity story — deliberately separate from `IdentityApi`, which owns *customer* identity (CIAM-shaped: self-registered, KYC-gated, large/unpredictable count). StaffIdentityApi is the opposite shape: a handful of staff accounts, provisioned by an administrator, optimized for least-privilege and audit, never self-registered (T-026 Q1).

**Its one caller is `OpsApi`** (the ops console's backend-for-frontend, not yet designed — `garbati-platform` T-028). No browser ever calls this service directly, and no other GARBATI service calls it either — `IdentityApi` and `StaffIdentityApi` never call each other (P-15; anything needing both goes through `OpsApi`, the same "orchestrates, owns no truth" role `OrchestratorApi` plays on the customer path).

### 1.1 What this service deliberately does NOT own

- **Customer identity, KYC, mandates** — `IdentityApi`'s job, unchanged by this document.
- **The staff-facing browser session** (the actual login cookie a staff member's browser holds) — `OpsApi`'s job (`ARCHITECTURE.md` 3.1). StaffIdentityApi answers "were these credentials correct, and what role does this person hold" on a single backend-to-backend call; it has no concept of a browser, a cookie, or a session lifetime of its own.
- **Maker-checker / separation-of-duties enforcement** (e.g. "the staff member who requested a reversal cannot also approve it") — **owner's decision this round: this lives in `OpsApi`**, not here. StaffIdentityApi only ever reports facts (who, what role); it has no concept of "this specific reversal request" or any other business workflow. `OpsApi` is the thing tracking who-did-what across a workflow, the same way `OrchestratorApi` tracks in-flight operation state for customer flows without owning any of the underlying truth.

### 1.2 Explicitly out of scope for v1 (owner's decisions this round, plus T-026's "note for later" flags)

| Item | Why deferred |
|---|---|
| More than one role per staff member | Owner's decision, 19 Oct 2026: one role per staff member for v1 — simplest model, matches T-026's "build now: role = permission set." Revisit only if a real need shows up. |
| MFA | T-026 flags this as worth having for privileged actions, without a concrete mechanism pinned down. Noted, not built. |
| Branch/jurisdiction-scoped roles | T-026 Q4: realistic need, but unclear GARBATI is even multi-branch yet. Noted, not built. |
| JIT / break-glass temporary elevation | T-026 Q4: meaningful once staff count and shift patterns justify it. Noted, not built. |
| SSO / external directory (AD/LDAP/SAML/OIDC) | T-026 Q1/Q3: the standard *enterprise-scale* upgrade, not a day-one requirement for a small shop. Local staff accounts are the v1 starting point. |
| Formal role-change-approval workflow | T-026 Q4: reasonable at a larger staff count; overhead for the first few staff accounts an owner/administrator creates directly. |
| Checking new passwords against known-breached-password lists | A real, cheap-to-want feature (NIST 800-63B recommends it), but it needs an external dataset/service (e.g. a HaveIBeenPwned-style API) — a new external dependency this document is not the place to introduce unasked. Noted for a future, explicitly-scoped task. |

## 2. Entities

### 2.1 `StaffUser`

| Field | Notes |
|---|---|
| `StaffId` | Public id, generated (`STF-XXXXXXXXXX`, same shape as `IdentityApi`'s `CUS-...`/`OrchestratorApi`'s `OPN-...`). |
| `FullName` | |
| `Username` | Unique, case-insensitive. Not an email — this is an internal account, not tied to a customer-style contact channel. |
| `PasswordHash` | ASP.NET Core Identity's `PasswordHasher<StaffUser>` (PBKDF2-HMAC-SHA256) — owner's decision this round; see 2.3. |
| `Role` | One of the fixed set in 2.2. Single value (owner's decision, 1.2). |
| `Status` | `Active` / `Suspended` / `Terminated`. `Terminated` is permanent — no path back to `Active`, mirroring `IdentityApi`'s `Closed`/`Deceased` being terminal (its decision #9). `Suspended` can be reactivated to `Active`. |
| `MustChangePassword` | Boolean. Set `true` whenever an administrator sets/resets someone else's password; cleared once the staff member changes it themselves on next login. Standard practice: an admin-set password is never meant to be the permanent one. |
| `SupervisorStaffId` | Nullable, self-referencing (`StaffId` of another `StaffUser`). Owner's decision, 19 Oct 2026 — see decision #4. |
| `CreatedAt`, `CreatedByStaffId` | `CreatedByStaffId` nullable — null only for the very first seeded administrator account (2.5). |
| `UpdatedAt` | |

### 2.2 `Role` (fixed enum for v1, from T-026 Q2 — `CustomerServiceOfficer` added by owner, 19 Oct 2026)

| Role | Primary responsibility (context only — `OpsApi` enforces the actual permission mapping, T-028) |
|---|---|
| `Teller` | Front-line transaction processing (fund/transfer initiation via the testing console). |
| `CustomerServiceOfficer` | Customer detail updates — the "account detail updates" item in the ops console's v1 scope (`TASKS.md` D-13) belongs to this role. |
| `Supervisor` | Approves overrides/exceptions; the "checker" in maker-checker for both `Teller` and `CustomerServiceOfficer` actions (decision #4 — the two roles share this supervisory relationship explicitly, not just by coincidence). |
| `ComplianceOfficer` | KYC/AML review. |
| `RiskOfficer` | Operations/risk review. |
| `Auditor` | Independent, read-only review — never a maker or a checker for any action. |
| `Administrator` | Staff and role management; system administration. Reserved for deployment/setup activity, not a default working account (mirrors Temenos's own convention, T-026 Q2). |

This document does not define what each role may actually *do* in the ops console beyond the context notes above — the actual permission mapping is `OpsApi`'s authorization logic (T-028), reading this service's `Role` value as its input. StaffIdentityApi's job stops at reporting the role (and, from decision #4 below, the supervisor).

### 2.3 Why a signed assertion (like IdentityApi's grant) is deliberately NOT used here

`IdentityApi`'s authorization grant is a short-lived, signed token specifically because it has to travel **through an intermediary** (`OrchestratorApi`) and be verified **independently, by a different service** (`LedgerApi`) without a round trip back to `IdentityApi` (P-12's whole point: no added latency, no new dependency on the issuer being reachable at verification time).

`StaffIdentityApi` → `OpsApi` has no such shape: `OpsApi` calls `StaffIdentityApi` directly, over a trusted backend-to-backend link, and is itself the only consumer of the answer — there is no third service that later needs to independently verify a token without calling back. Reusing the signed-grant pattern here would be copying a mechanism without the constraint that justifies it. **Decision #1:** `POST /v1/staff/authenticate` returns a plain, ordinary authenticated JSON response (Section 3) — no JWT, no signing key, no new cryptographic machinery in this service at all. `OpsApi` is the one that mints the actual browser-facing session (an opaque, server-side session id in an `HttpOnly`/`Secure`/`SameSite` cookie, per `ARCHITECTURE.md` 3.1) — entirely `OpsApi`'s own concern, out of scope here.

### 2.4 Supervisory relationship (decision #4, owner, 19 Oct 2026)

A real org chart, not just a flat role label: `StaffUser.SupervisorStaffId` names *which specific staff member* a given `Teller` or `CustomerServiceOfficer` reports to. The owner's own example — a `Teller` and a `CustomerServiceOfficer` sharing the same supervisor — is an organizational fact an `Administrator` configures per staff member, not a rule this service hardcodes; it is naturally expressed by assigning the same `SupervisorStaffId` to both.

**Validation (enforced at assignment time, matching T-026 Q4's "SoD rules checked at assignment time, not just at action time"):**
- `SupervisorStaffId` must reference an existing, `Active` `StaffUser` whose `Role` is `Supervisor`. An `Administrator` is a different functional role (system/staff administration, not line supervision) and is deliberately not accepted here — if that turns out to be too strict in practice, it's a cheap rule to relax later, and relaxing a validation rule is a much smaller change than discovering too late that "supervisor" silently meant two different things.
- A staff member cannot be their own supervisor.
- `Supervisor`, `ComplianceOfficer`, `RiskOfficer`, `Auditor`, and `Administrator` accounts have no `SupervisorStaffId` of their own in v1 (`null`) — only `Teller` and `CustomerServiceOfficer` are supervised line roles in this version. This is a deliberate simplification, not a claim that supervisors/compliance/risk/audit roles never report to anyone in reality; a fuller org chart (e.g. a `Supervisor` reporting to a branch manager) is out of scope until there's a real need for it.

**What this does NOT do:** `SupervisorStaffId` is a fact StaffIdentityApi reports; it does not, by itself, make `OpsApi` enforce anything. Whether a `Teller`'s or `CustomerServiceOfficer`'s specific action actually requires their named supervisor's approval (versus any `Supervisor`) is `OpsApi`'s call (T-028) — flagged here as a concrete design question for that task, not decided by this document.

**Documented v1 limitation (`garbati-platform` D-17, decided by the owner 20 Oct 2026, found by T-032's QA review):** the validation above runs only when `SupervisorStaffId` itself is written (`PATCH /v1/staff/{staffId}/supervisor`) — it is **not** re-checked when the *referenced* staff member's own role later changes. If a `Supervisor` is reassigned to, say, `ComplianceOfficer`, every `Teller`/`CustomerServiceOfficer` who still has `SupervisorStaffId` pointing at them keeps pointing at someone no longer eligible — silently, with no flag, no block, and no automatic cleanup. This is a direct, accepted consequence of the "assignment-time-only" choice above (T-026 Q4), not an oversight discovered after the fact: the owner was asked explicitly, after QA surfaced it, whether to fix this now (block the role change while active reports exist, or cascade-clear them) and chose to leave it as a known v1 limitation instead, on the reasoning that the real exposure is low until real staff accounts and real traffic exist. Revisit if that stops being true.

### 2.5 `StaffAuditLog`

Same append-only convention every GARBATI service follows (`ARCHITECTURE.md` Section 5): one row per sensitive action, written in the same database transaction as the change it describes.

| Field | Notes |
|---|---|
| `Id` | |
| `Action` | `StaffCreated`, `RoleChanged`, `StatusChanged`, `SupervisorChanged`, `PasswordReset`, `AuthenticationSucceeded`, `AuthenticationFailed`. |
| `TargetStaffId` | Whose account this is about. |
| `ActingStaffId` | Who did it. Nullable **only** for `AuthenticationFailed` (the attempted username is recorded in `Details`, never the password) and for the very first seeded administrator's own `StaffCreated` row. |
| `Details` | Free text — e.g. old role → new role, old status → new status. Never a password or password hash. |
| `CreatedAt` | |

**Decision #2:** the very first `Administrator` account is created by a one-time seed (migration or startup check, mirroring `LedgerApi`'s pattern of seeding system accounts it can't otherwise bootstrap) — not through `POST /v1/staff`, since that endpoint requires an already-authenticated Administrator caller, and the first one has to come from somewhere. Seeded with `MustChangePassword = true` and a credential delivered out-of-band (not committed to source control, not logged) — the owner sets the real initial password at deployment time, the same way `prod-signing-key.pem`-style secrets are handled elsewhere in this platform.

## 3. Endpoints (v1)

All under `/v1/staff`, following `ARCHITECTURE.md` Section 5's shared conventions (versioning, error shapes, correlation id, health checks) without deviation.

| Endpoint | Scope required | Notes |
|---|---|---|
| `POST /v1/staff/authenticate` | `staffidentity.authenticate` | `{username, password}` → `200 {staffId, fullName, role}` on success, `401` (generic — no "wrong username" vs "wrong password" distinction, to avoid username enumeration) on failure. Every attempt is audited (2.5), success or failure. If `Status != Active`, authentication fails exactly like a wrong password (same generic `401`) — a suspended/terminated account gives no signal distinguishing it from a bad credential. |
| `POST /v1/staff` | `staffidentity.admin` | Create a staff account. `{idempotencyKey, actingStaffId, fullName, username, role}` — no password in the request; a one-time temporary credential is generated server-side and returned once in the response (never stored in plain text, never logged), with `MustChangePassword = true`. Idempotency key required (`ARCHITECTURE.md` Section 5), same convention as every other create-something-new endpoint on this platform. |
| `GET /v1/staff/{staffId}` | `staffidentity.admin` | Never returns `PasswordHash`. Includes `supervisorStaffId`. |
| `GET /v1/staff?supervisorStaffId=...` | `staffidentity.admin` | Paged list/search, for the staff & role management screen. The `supervisorStaffId` filter answers "who reports to this supervisor" — needed once `OpsApi` (T-028) decides whether a `Supervisor` should only see their own reports' pending approvals. |
| `PATCH /v1/staff/{staffId}/role` | `staffidentity.admin` | `{actingStaffId, role}`. Audited. Changing a `Teller`/`CustomerServiceOfficer` to any other role does **not** automatically clear `SupervisorStaffId` — a separate `PATCH .../supervisor` call is required, so the two changes are always independently audited rather than one silently implying the other. |
| `PATCH /v1/staff/{staffId}/status` | `staffidentity.admin` | `{actingStaffId, status}`. Audited. Rejects any transition out of `Terminated` (400). |
| `PATCH /v1/staff/{staffId}/supervisor` | `staffidentity.admin` | `{actingStaffId, supervisorStaffId}` (nullable, to unassign). Audited. Rejects (400) unless `supervisorStaffId` is `null` or references an `Active` `Supervisor` (decision #4); rejects self-reference. |
| `POST /v1/staff/{staffId}/password` | `staffidentity.admin` (admin-driven reset) or `staffidentity.authenticate` (self-service change, caller supplies their own current password) | Two distinct real-world actions sharing one endpoint shape, distinguished by which scope/credential the caller presents — mirrors how `IdentityApi`'s `AuthorizationController.RequestAuthorization` already branches one endpoint on caller scope (its own T-020 comment makes the same call). Admin-driven reset sets `MustChangePassword = true`; self-service change clears it. |

**Decision #3 (two scopes, not one):** `staffidentity.authenticate` (login only) and `staffidentity.admin` (create/list/modify staff and roles) are kept separate, even though `OpsApi` is the only caller today and needs both. This limits blast radius if a future internal caller only ever needs to verify a login and should never be able to create or modify staff accounts — cheap to split now, costly to retrofit later. (Contrast with `OrchestratorApi`'s own judgment call to use a *single* scope per caller elsewhere in this platform, Appendix A item 3 of its design — that call was right for a service whose one caller has one coherent job; staff identity's "authenticate" and "administer" are genuinely different blast radii for the same caller.)

## 4. Password policy (v1)

- Minimum 12 characters. No composition rules (no forced mix of upper/lower/digit/symbol) — current NIST 800-63B guidance deprioritizes composition rules in favor of length, and composition rules are a well-documented source of weaker, more predictable passwords in practice.
- Hashed with ASP.NET Core Identity's `PasswordHasher<StaffUser>` (PBKDF2-HMAC-SHA256) — owner's decision this round. This is the one new package dependency this service introduces (`Microsoft.AspNetCore.Identity`, used only for its password hasher — no cookie middleware, no `UserManager`, no other part of ASP.NET Core Identity's much larger surface is pulled in).
- No breached-password check in v1 (1.2).

## 5. What "done" looks like for the first build task

A follow-up task (not this document) builds: the `StaffUser`/`StaffAuditLog` EF Core entities and migration (including the one-time Administrator seed, decision #2), the endpoints in Section 3, `ApiKeyAuthenticationHandler`-style scope enforcement (reusing the exact pattern already proven in `LedgerApi`/`IdentityApi`/`OrchestratorApi`, not inventing a new one), and a test suite covering: successful/failed authentication (including the generic-401 non-enumeration behavior), status transitions (including the `Terminated`-is-permanent rule), the two-scope split, idempotent staff creation, and the supervisor-assignment validation rules in decision #4 (must reference an `Active` `Supervisor`, no self-reference, independence from `PATCH .../role`).

## 6. Open questions (explicitly deferred, not silently skipped)

1. Session/credential lifetime on the `OpsApi` side (how long a staff login session lasts before re-authentication is required) is `OpsApi`'s decision, not this service's — flagged here only so it isn't lost: T-028 needs to decide it.
2. Whether `staffidentity.admin` should itself be split further (e.g. a narrower scope for "change status" vs "create new account") is a real question once a second privileged caller (if any) ever exists; not worth the complexity for a single caller today.
3. CBN or any Nigeria-specific staff-access/authentication regulatory requirement — T-026 found none in general research; a compliance officer should confirm before this goes anywhere near real production use with real staff.
4. Whether `OpsApi` should require a `Teller`'s/`CustomerServiceOfficer`'s *own named* supervisor to approve their actions, versus any `Supervisor` — decision #4 only establishes the fact (who supervises whom); T-028 decides what, if anything, that fact gates.
5. **Resolved, not deferred:** whether a Supervisor-reassignment should clean up orphaned reports pointing at them (D-17) — the owner decided to leave this as a documented limitation rather than fix it now. See 2.4's own note for the full reasoning; listed here only as a pointer so a reader scanning this section doesn't miss it.

## Appendix A. T-032 build notes (foundation: entities, all 8 endpoints, Administrator seed)

Built everything in this document's v1 scope in one task, on `feat/foundation`, mirroring `LedgerApi`/`Garbati.IdentityApi`/`Garbati.OrchestratorApi`'s actual implementation for every cross-cutting concern. The judgment calls the task file left open, plus the smaller decisions made along the way:

1. **Administrator seed bootstrap mechanism (decision #2) — reuse endpoint 8, don't invent a ninth one.** The task file offered two shapes: (a) a known-invalid placeholder hash plus an out-of-band "set initial admin password" script/endpoint, or (b) generating the password from an environment variable read only at migration-apply time. Chose a close variant of (a): the migration (`Migrations/*SeedInitialAdministrator*`) inserts the one Administrator row with a deliberately invalid placeholder hash (32 bytes of `0xFF`, base64-encoded — not a format `PasswordHasher<TUser>.VerifyHashedPassword` recognizes, so it safely returns `Failed` for every password, forever, without throwing) and `MustChangePassword = true`. No password, generated or chosen, is ever baked into the committed migration file. The "out-of-band mechanism" is simply **this document's own endpoint 8, admin branch** — `POST /v1/staff/{staffId}/password`, called once by an operator holding the platform's `staffidentity.admin` service credential, against the seeded row's own `StaffId`. Rejected (b): reading a secret inside a migration's `Up()` only at apply-time is real, but it would need its own, novel plumbing (an env var the migrator image must be given, with no existing precedent on this platform to copy) and would be awkward to test end-to-end in CI without either committing a test secret or special-casing the Testcontainers fixture. Rejected inventing a dedicated ninth endpoint for (a) literally: `docs/DESIGN.md` Section 3's table is this service's literal spec, and endpoint 8's admin branch already does exactly what a bootstrap step needs (generate a fresh one-time credential server-side, return it once, require nothing about the account's current password) — adding a second, parallel mechanism would be maintaining two ways to do the same thing. `tests/.../Controllers/AdministratorSeedTests.cs` proves this end-to-end: the seeded row can authenticate with *no* password until reset, the reset call succeeds and returns a usable credential, and authenticating with that credential afterward actually succeeds.
2. **Username case-insensitive uniqueness: a normalized shadow column, not `citext`.** No sibling service's database already depends on the Postgres `citext` extension for a similar constraint (checked `IdentityDbContext`'s/`LedgerDbContext`'s own unique indexes — none exist for a case-insensitive text column), so there was no existing convention to match either way. A plain `NormalizedUsername` column (`Username.Trim().ToUpperInvariant()`, written by `StaffService` on create) with its own unique index needs no Postgres extension to be installed/available on whatever instance this gets deployed to, and is the same pattern ASP.NET Core Identity's own `NormalizedUserName` uses — a natural fit alongside already using `PasswordHasher<TUser>` from that same package family.
3. **`StaffUser` carries three columns `docs/DESIGN.md` Section 2.1 doesn't name: `IdempotencyKey`, `CreatedByClientId`, `RequestHash`.** Same precedent as `OrchestratorApi`'s `AccountOpening` entity carrying `IdempotencyKey`/`InitiatedBy`/`RequestHash` beyond what its own design doc's entity prose lists — `POST /v1/staff`'s idempotency requirement (`ARCHITECTURE.md` Section 5) has to be stored somewhere, and putting it directly on the entity it protects, scoped by the calling API client, is this platform's established shape for that, not a new one invented here.
4. **Endpoint 8's admin branch generates its own one-time credential; it never accepts a caller-supplied new password.** `docs/DESIGN.md` Section 3's own text for endpoint 8 says only "admin-driven reset sets `MustChangePassword = true`" — it does not spell out whether the admin supplies the new password or one is generated. Made this symmetric with endpoint 2 (`POST /v1/staff`, which explicitly never takes a password in the request): an admin-scoped caller never puts a plaintext password for someone else over the wire anywhere in this service. `ChangeStaffPasswordRequest.NewPassword` is simply ignored on this branch.
5. **Two Terminated-adjacent rules, spelled out precisely.** "Rejects (400) ANY transition out of Terminated" is read as "away from Terminated to something else" — a `Terminated → Terminated` call (repeating the current status) is treated as the idempotent no-op this platform uses elsewhere for "repeat of an already-successful thing," not as a rejected transition. More generally, `ChangeStatusAsync` treats *any* same-status PATCH as a no-op (no audit row, no `UpdatedAt` bump), not just the Terminated case — a single, general rule rather than one special-cased exception.
6. **Test strategy: controller-level (HTTP), against the real app and a real Testcontainers Postgres, with nothing mocked.** Unlike `OrchestratorApi`, this service calls no other service at all (`docs/DESIGN.md` Section 1), so there is no `Clients/`-style fake to maintain and no "what this doesn't prove" gap about a sibling's contract drifting — the whole suite exercises the real pipeline end-to-end against a real database. One shared `ApiFixture`/one Postgres container for the whole run (not a separate `PostgresFixture` the way `OrchestratorApi` needed for its own lower-level, multi-step service tests) — this service's business logic is simple enough that HTTP-level tests already cover it fully.
7. **`ChangePassword`'s dual-scope branching is a structural exception, not a second controller.** Mirrors `Garbati.IdentityApi`'s `AuthorizationController.RequestAuthorization` exactly: `[Authorize(Policy=...)]` is evaluated before the action runs, so a single static attribute cannot pick between `staffidentity.admin` and `staffidentity.authenticate` depending on which one the caller's credential holds. The action uses plain `[Authorize]` plus an explicit `User.HasClaim` check instead, and `ControllerAuthorizationTests`' usual "every action needs exactly one scope policy" structural test carries the same named exception IdentityApi's own equivalent test does.

See the PR description for how this was tested, the exact passing test count, what could not be verified, and the full LEARNING_LOG entry.
