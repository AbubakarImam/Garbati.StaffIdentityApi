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
| `CreatedAt`, `CreatedByStaffId` | `CreatedByStaffId` nullable — null only for the very first seeded administrator account (2.4). |
| `UpdatedAt` | |

### 2.2 `Role` (fixed enum for v1, from T-026 Q2)

`Teller`, `Supervisor`, `ComplianceOfficer`, `RiskOfficer`, `Auditor`, `Administrator`.

This document does not define what each role may actually *do* in the ops console — that's `OpsApi`'s authorization logic (T-028), reading this service's `Role` value as its input. StaffIdentityApi's job stops at reporting the role.

### 2.3 Why a signed assertion (like IdentityApi's grant) is deliberately NOT used here

`IdentityApi`'s authorization grant is a short-lived, signed token specifically because it has to travel **through an intermediary** (`OrchestratorApi`) and be verified **independently, by a different service** (`LedgerApi`) without a round trip back to `IdentityApi` (P-12's whole point: no added latency, no new dependency on the issuer being reachable at verification time).

`StaffIdentityApi` → `OpsApi` has no such shape: `OpsApi` calls `StaffIdentityApi` directly, over a trusted backend-to-backend link, and is itself the only consumer of the answer — there is no third service that later needs to independently verify a token without calling back. Reusing the signed-grant pattern here would be copying a mechanism without the constraint that justifies it. **Decision #1:** `POST /v1/staff/authenticate` returns a plain, ordinary authenticated JSON response (2.4) — no JWT, no signing key, no new cryptographic machinery in this service at all. `OpsApi` is the one that mints the actual browser-facing session (an opaque, server-side session id in an `HttpOnly`/`Secure`/`SameSite` cookie, per `ARCHITECTURE.md` 3.1) — entirely `OpsApi`'s own concern, out of scope here.

### 2.4 `StaffAuditLog`

Same append-only convention every GARBATI service follows (`ARCHITECTURE.md` Section 5): one row per sensitive action, written in the same database transaction as the change it describes.

| Field | Notes |
|---|---|
| `Id` | |
| `Action` | `StaffCreated`, `RoleChanged`, `StatusChanged`, `PasswordReset`, `AuthenticationSucceeded`, `AuthenticationFailed`. |
| `TargetStaffId` | Whose account this is about. |
| `ActingStaffId` | Who did it. Nullable **only** for `AuthenticationFailed` (the attempted username is recorded in `Details`, never the password) and for the very first seeded administrator's own `StaffCreated` row. |
| `Details` | Free text — e.g. old role → new role, old status → new status. Never a password or password hash. |
| `CreatedAt` | |

**Decision #2:** the very first `Administrator` account is created by a one-time seed (migration or startup check, mirroring `LedgerApi`'s pattern of seeding system accounts it can't otherwise bootstrap) — not through `POST /v1/staff`, since that endpoint requires an already-authenticated Administrator caller, and the first one has to come from somewhere. Seeded with `MustChangePassword = true` and a credential delivered out-of-band (not committed to source control, not logged) — the owner sets the real initial password at deployment time, the same way `prod-signing-key.pem`-style secrets are handled elsewhere in this platform.

## 3. Endpoints (v1)

All under `/v1/staff`, following `ARCHITECTURE.md` Section 5's shared conventions (versioning, error shapes, correlation id, health checks) without deviation.

| Endpoint | Scope required | Notes |
|---|---|---|
| `POST /v1/staff/authenticate` | `staffidentity.authenticate` | `{username, password}` → `200 {staffId, fullName, role}` on success, `401` (generic — no "wrong username" vs "wrong password" distinction, to avoid username enumeration) on failure. Every attempt is audited (2.4), success or failure. If `Status != Active`, authentication fails exactly like a wrong password (same generic `401`) — a suspended/terminated account gives no signal distinguishing it from a bad credential. |
| `POST /v1/staff` | `staffidentity.admin` | Create a staff account. `{idempotencyKey, actingStaffId, fullName, username, role}` — no password in the request; a one-time temporary credential is generated server-side and returned once in the response (never stored in plain text, never logged), with `MustChangePassword = true`. Idempotency key required (`ARCHITECTURE.md` Section 5), same convention as every other create-something-new endpoint on this platform. |
| `GET /v1/staff/{staffId}` | `staffidentity.admin` | Never returns `PasswordHash`. |
| `GET /v1/staff` | `staffidentity.admin` | Paged list/search, for the staff & role management screen. |
| `PATCH /v1/staff/{staffId}/role` | `staffidentity.admin` | `{actingStaffId, role}`. Audited. |
| `PATCH /v1/staff/{staffId}/status` | `staffidentity.admin` | `{actingStaffId, status}`. Audited. Rejects any transition out of `Terminated` (400). |
| `POST /v1/staff/{staffId}/password` | `staffidentity.admin` (admin-driven reset) or `staffidentity.authenticate` (self-service change, caller supplies their own current password) | Two distinct real-world actions sharing one endpoint shape, distinguished by which scope/credential the caller presents — mirrors how `IdentityApi`'s `AuthorizationController.RequestAuthorization` already branches one endpoint on caller scope (its own T-020 comment makes the same call). Admin-driven reset sets `MustChangePassword = true`; self-service change clears it. |

**Decision #3 (two scopes, not one):** `staffidentity.authenticate` (login only) and `staffidentity.admin` (create/list/modify staff and roles) are kept separate, even though `OpsApi` is the only caller today and needs both. This limits blast radius if a future internal caller only ever needs to verify a login and should never be able to create or modify staff accounts — cheap to split now, costly to retrofit later. (Contrast with `OrchestratorApi`'s own judgment call to use a *single* scope per caller elsewhere in this platform, Appendix A item 3 of its design — that call was right for a service whose one caller has one coherent job; staff identity's "authenticate" and "administer" are genuinely different blast radii for the same caller.)

## 4. Password policy (v1)

- Minimum 12 characters. No composition rules (no forced mix of upper/lower/digit/symbol) — current NIST 800-63B guidance deprioritizes composition rules in favor of length, and composition rules are a well-documented source of weaker, more predictable passwords in practice.
- Hashed with ASP.NET Core Identity's `PasswordHasher<StaffUser>` (PBKDF2-HMAC-SHA256) — owner's decision this round. This is the one new package dependency this service introduces (`Microsoft.AspNetCore.Identity`, used only for its password hasher — no cookie middleware, no `UserManager`, no other part of ASP.NET Core Identity's much larger surface is pulled in).
- No breached-password check in v1 (1.2).

## 5. What "done" looks like for the first build task

A follow-up task (not this document) builds: the `StaffUser`/`StaffAuditLog` EF Core entities and migration (including the one-time Administrator seed, decision #2), the endpoints in Section 3, `ApiKeyAuthenticationHandler`-style scope enforcement (reusing the exact pattern already proven in `LedgerApi`/`IdentityApi`/`OrchestratorApi`, not inventing a new one), and a test suite covering: successful/failed authentication (including the generic-401 non-enumeration behavior), status transitions (including the `Terminated`-is-permanent rule), the two-scope split, and idempotent staff creation.

## 6. Open questions (explicitly deferred, not silently skipped)

1. Session/credential lifetime on the `OpsApi` side (how long a staff login session lasts before re-authentication is required) is `OpsApi`'s decision, not this service's — flagged here only so it isn't lost: T-028 needs to decide it.
2. Whether `staffidentity.admin` should itself be split further (e.g. a narrower scope for "change status" vs "create new account") is a real question once a second privileged caller (if any) ever exists; not worth the complexity for a single caller today.
3. CBN or any Nigeria-specific staff-access/authentication regulatory requirement — T-026 found none in general research; a compliance officer should confirm before this goes anywhere near real production use with real staff.
