# Garbati.StaffIdentityApi

GARBATI's staff/workforce identity service — who is logging into the bank's internal ops console, and what role do they hold. Built with ASP.NET Core 8, EF Core and PostgreSQL, matching the rest of the platform.

This is deliberately a separate identity domain from [Garbati.IdentityApi](https://github.com/AbubakarImam/Garbati.IdentityApi), which owns *customer* identity (KYC, mandates). Real core-banking platforms keep staff/workforce identity and customer identity (CIAM) structurally distinct — see `research/staff-ops-identity-patterns.md` in [garbati-platform](https://github.com/AbubakarImam/garbati-platform) for why, and `garbati-platform/TASKS.md` D-13 / `ARCHITECTURE.md` P-15 for the ratified decision.

The design, including every trade-off, is in [docs/DESIGN.md](docs/DESIGN.md) — start there for the *why*. See [`garbati-platform/ARCHITECTURE.md`](https://github.com/AbubakarImam/garbati-platform/blob/main/ARCHITECTURE.md) Section 3.1 for how this service fits the ops-console flow: **Ops console (browser) → OpsApi → StaffIdentityApi / IdentityApi / OrchestratorApi / LedgerApi**.

## What this build covers

This is StaffIdentityApi's **first** build task (`garbati-platform` T-032) and its entire v1 scope in one go — `docs/DESIGN.md`'s whole point is that this version is deliberately minimal. It covers:

- `StaffUser` / `StaffAuditLog` entities, migrations (including the one-time Administrator seed).
- All 8 endpoints in `docs/DESIGN.md` Section 3: authenticate, create, get, list, role/status/supervisor PATCH, password (admin reset or self-service change, branched on scope).
- API-key authentication, two scopes (`staffidentity.authenticate`, `staffidentity.admin`), audit logging, health checks.

This service makes **no outbound calls of its own** — it is the opposite shape from `OrchestratorApi`: nothing to mock in tests, nothing to configure as a downstream dependency. Its one caller, `OpsApi`, is not built yet.

## Core pieces

- **StaffUser** — `StaffId` (public, generated, `STF-XXXXXXXXXX`), `Username` (unique, case-insensitive via a normalized shadow column + a real unique index), `PasswordHash` (`PasswordHasher<StaffUser>`, PBKDF2-HMAC-SHA256), `Role` (one of 7 fixed values, stored as a string), `Status` (`Active`/`Suspended`/`Terminated` — `Terminated` is a true dead end), `MustChangePassword`, `SupervisorStaffId` (nullable, self-referencing FK to another `StaffUser.StaffId`).
- **StaffAuditLog** — one row per sensitive action (`StaffCreated`, `RoleChanged`, `StatusChanged`, `SupervisorChanged`, `PasswordReset`, `AuthenticationSucceeded`, `AuthenticationFailed`), staged on the same `DbContext` as the change it describes.
- **Administrator seed** — a migration inserts exactly one `Administrator` row with a deliberately invalid password hash and `MustChangePassword = true`. See `docs/DESIGN.md` Appendix A for the full reasoning and how it's bootstrapped.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (pinned by `global.json`)
- [Docker](https://www.docker.com/) (for the local database and for the tests)

## Getting started

**1. Start PostgreSQL.** The Development connection string expects it on port `5436` (LedgerApi uses `5433`, IdentityApi `5434`, OrchestratorApi `5435`):

```bash
docker run -d --name staffidentity-postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=staffidentityapi -p 5436:5432 postgres:16-alpine
```

**2. Apply the migrations.** `dotnet-ef` is pinned in the repo's tool manifest:

```bash
dotnet tool restore
dotnet ef database update --project src/Garbati.StaffIdentityApi
```

This also runs the one-time Administrator seed (see [Bootstrapping the seeded Administrator](#bootstrapping-the-seeded-administrator-dev-local) below).

**3. Run the API:**

```bash
dotnet run --project src/Garbati.StaffIdentityApi --launch-profile http
```

Swagger UI is at the printed `http://localhost:{port}/swagger` (click **Authorize** and paste `dev-ops-api-key` — the plaintext behind the dev client configured in `appsettings.Development.json`).

## Running the tests

```bash
dotnet test
```

The whole suite runs against a real PostgreSQL container started by [Testcontainers](https://dotnet.testcontainers.org/) (Docker must be running) and the real app in memory (`WebApplicationFactory`) — no mocks, since this service calls nothing downstream.

## Endpoints

All under `/v1/staff` (`docs/DESIGN.md` Section 3):

| Method | Route | Scope | Purpose |
|---|---|---|---|
| `POST` | `/v1/staff/authenticate` | `staffidentity.authenticate` | `{username, password}` → `200 {staffId, fullName, role}`, or a single generic `401` for any failure (wrong username, wrong password, non-`Active` status) |
| `POST` | `/v1/staff` | `staffidentity.admin` | Creates a staff account. No password in the request — a one-time temporary credential is generated and returned once |
| `GET` | `/v1/staff/{staffId}` | `staffidentity.admin` | Never returns `passwordHash` |
| `GET` | `/v1/staff?supervisorStaffId=...` | `staffidentity.admin` | Paged list; filters by supervisor when given |
| `PATCH` | `/v1/staff/{staffId}/role` | `staffidentity.admin` | Does **not** touch `supervisorStaffId` |
| `PATCH` | `/v1/staff/{staffId}/status` | `staffidentity.admin` | Rejects (400) any transition out of `Terminated` |
| `PATCH` | `/v1/staff/{staffId}/supervisor` | `staffidentity.admin` | Rejects (400) unless null or an existing, `Active`, `Supervisor`-role staff member; rejects self-reference |
| `POST` | `/v1/staff/{staffId}/password` | `staffidentity.admin` **or** `staffidentity.authenticate` | Admin branch: resets to a fresh one-time credential, sets `mustChangePassword`. Self-service branch: verifies the caller's current password, clears the flag |

### Status codes

| Status | Meaning |
|---|---|
| `200` | Success |
| `400` | Request failed validation, an invalid status transition out of `Terminated`, an invalid supervisor assignment, or an incorrect current password on self-service change |
| `401` | Missing/unknown API key, or any authentication failure on `POST /v1/staff/authenticate` |
| `403` | The API key lacks the endpoint's scope |
| `404` | Staff member not found |
| `409` | Idempotency key reused with a different request body, or a username already in use |
| `500` | Unexpected error |

### Idempotency

`POST /v1/staff` requires a client-supplied `idempotencyKey` (1-128 characters: letters, digits, `-`, `_`, `.` or `:`), scoped to the calling API client — the same scheme every other create-something-new endpoint on this platform uses. A retry with the same key and the same other fields returns the same staff account **without** the one-time temporary credential (only the original call ever returns it); the same key with different fields gets **409**.

## Authentication

Same pattern as every sibling service (`ARCHITECTURE.md` Section 5): every request needs an `X-Api-Key` header, and each key is granted **scopes**:

| Scope | Covers |
|---|---|
| `staffidentity.authenticate` | `POST /v1/staff/authenticate`; the self-service branch of `POST .../password` |
| `staffidentity.admin` | Create/get/list/role/status/supervisor; the admin-driven-reset branch of `POST .../password` |

Kept deliberately separate even though `OpsApi` (the one caller) needs both — see `docs/DESIGN.md` Section 3 decision #3. A missing or unknown key gets **401**; a valid key without the endpoint's scope gets **403**.

### Development keys

`appsettings.Development.json` configures one local-only client. Never use this outside your machine.

| Client | Plaintext key (dev only) | Scopes |
|---|---|---|
| `dev-ops-api` | `dev-ops-api-key` | `staffidentity.authenticate`, `staffidentity.admin` |

### Issuing a key

Only the SHA-256 hash of a key is configured, never the key itself:

```bash
KEY="sik_$(openssl rand -base64 32 | tr '+/' '-_' | tr -d '=')"; echo "$KEY"; printf %s "$KEY" | openssl dgst -sha256 -binary | base64
```

## Bootstrapping the seeded Administrator (dev/local)

The migrations seed exactly one `Administrator` (`STF-0000000001`, username `admin`) with an intentionally invalid password hash — it cannot log in with any password until reset. After applying migrations, make it usable with one admin-scoped call (see `docs/DESIGN.md` Appendix A for why this, and not a new endpoint or an env-var secret, was chosen):

```bash
curl -X POST http://localhost:{port}/v1/staff/STF-0000000001/password \
  -H "X-Api-Key: dev-ops-api-key" -H "Content-Type: application/json" \
  -d '{"actingStaffId":"STF-0000000001"}'
```

The response's `temporaryPassword` is shown once — note it down, then authenticate as `admin` with it and change it via the self-service branch.

## Logging and audit

Every request gets a correlation id (`X-Correlation-ID`). Audit rows (`staff_audit_logs`) are written in the same database transaction as the change they describe. No password, password hash or one-time temporary credential is ever logged.

This is deliberately all GARBATI ships today — no metrics/tracing/alerting stack. See `garbati-platform`'s `OBSERVABILITY.md` for why, and what to reach for if you need more.

## Deployment

The `Dockerfile` builds two images:

```bash
docker build -t staffidentityapi .                                   # the API
docker build --target migrator -t staffidentityapi-migrator .        # applies pending migrations, then exits
```

GitHub Actions (`.github/workflows/ci.yml`) runs the tests and builds both images on every pull request and push to `main`. The `deploy` job is a placeholder (resource/app names copied from OrchestratorApi's, not yet real infrastructure) — it only runs on `main` and the owner wires up real values separately.

## Project layout

```
src/Garbati.StaffIdentityApi/
  Controllers/      StaffController (all 8 endpoints)
  Services/         StaffService (business logic), StaffIdGenerator, TemporaryCredentialGenerator
  Entities/         StaffUser, StaffAuditLog, Role, StaffStatus
  Data/             StaffIdentityDbContext and model configuration
  Migrations/       EF Core migrations, including the Administrator seed
  Validation/       Request validators, IdempotencyKeyRule, RoleRule
  Middleware/       Correlation id, exception-to-status mapping
  Auditing/         IStaffAuditLogger (stages rows on the request's DbContext)
  Authorization/    API-key authentication and scopes
  Configuration/    PasswordPolicyOptions
  Contracts/        Request/response records
  Exceptions/
tests/Garbati.StaffIdentityApi.Tests/
  Controllers/      End-to-end tests over the real app + Testcontainers Postgres
  Authorization/, Configuration/, Data/, Fixtures/, Validation/
docs/DESIGN.md      Design document, decisions log, and Appendix A (T-032 build notes)
```

## Out of scope

Everything `docs/DESIGN.md` Section 1.2/6 defers: more than one role per staff member, MFA, branch/jurisdiction scoping, JIT/break-glass access, SSO/external directory integration, a formal role-change-approval workflow, breached-password checking. Also out of scope: `OpsApi` itself (not built yet) — this service calls nothing and assumes nothing about it.
