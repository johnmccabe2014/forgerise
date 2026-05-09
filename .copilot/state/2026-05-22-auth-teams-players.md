# Phase 2 Plan — Auth, Teams, Players (2026-05-22)

**Master prompt:** [.copilot/master.prompt.md](../master.prompt.md) §8 (MVP), §10 (Security)
**Phase 1 verdict:** [2026-05-08-bootstrap.iter2.review.md](./2026-05-08-bootstrap.iter2.review.md) — Approved 38/40
**Owner agents:** backend (lead), security-engineer (gate), tester, principal-reviewer.

---

## Outcome

Coach can register, log in, log out, refresh a session, create a team, and add players — through a hardened auth surface that satisfies master prompt §10 in full.

## Non-goals (defer)

- Email verification flow (Phase 3 alongside attendance/welfare)
- Password reset (Phase 3)
- Multi-tenant org/club hierarchy (Phase 6)
- Social/oauth providers (post-MVP)
- Player self-service accounts (post-MVP — Phase 1 master prompt §1: coach-first)

## Tasks

### A · Data layer (PostgreSQL + EF Core 9)

| # | Task |
|---|---|
| A1 | Add `Npgsql.EntityFrameworkCore.PostgreSQL` 9.x to `ForgeRise.Api`. |
| A2 | `Data/AppDbContext.cs` with `Users`, `RefreshTokens`, `Teams`, `Players` DbSets. |
| A3 | Entity types: `User` (Id, Email-unique-CI, PasswordHash, DisplayName, CreatedAt), `RefreshToken` (Id, UserId-FK, TokenHash, ExpiresAt, RevokedAt, ReplacedByTokenId, CreatedAt, CreatedByIp), `Team` (Id, OwnerUserId-FK, Name, Code-unique-per-owner, CreatedAt), `Player` (Id, TeamId-FK, DisplayName, JerseyNumber?, BirthYear?, Position?, IsActive, CreatedAt). |
| A4 | EF migration `Initial` checked in under `api/ForgeRise.Api/Data/Migrations/`. |
| A5 | Connection string from `ConnectionStrings:Postgres` env; default points to `localhost:5432`. |
| A6 | docker-compose.yml at repo root with a `postgres:16-alpine` for local dev only (not deployed). |

### B · Password hashing (Argon2id)

| # | Task |
|---|---|
| B1 | Add `Konscious.Security.Cryptography.Argon2` 1.3.0. |
| B2 | `Auth/PasswordHasher.cs` — Argon2id, params: 19 MiB / 2 iterations / 1 lane (OWASP 2024). Salt 16 B, hash 32 B. Stored as `argon2id$v=19$m=...,t=...,p=...$base64salt$base64hash`. |
| B3 | Constant-time verify; rehash if parameters change. |
| B4 | Tests: round-trip, wrong password rejection, malformed-string rejection, parameter-change rehash. |

### C · JWT + refresh rotation

| # | Task |
|---|---|
| C1 | Add `Microsoft.AspNetCore.Authentication.JwtBearer` 9.x. |
| C2 | Symmetric HS256 signing, `Jwt:Key` (env), `Jwt:Issuer`, `Jwt:Audience`. Access lifetime 15 min, refresh 30 days. |
| C3 | `Auth/TokenService.cs`: issues access (claims: sub=UserId, email, jti), creates a refresh token (random 32 bytes b64url, stored as SHA-256 hash), enforces single-use rotation: refresh → revoke old, mint new, return new pair. Replay (using a revoked token) ⇒ revoke entire chain (`ReplacedByTokenId` walk) and 401. |
| C4 | Tests: rotate, replay-detection, expired refresh, revoked refresh. |

### D · HttpOnly cookies + CSRF

| # | Task |
|---|---|
| D1 | Cookie names: `fr_at` (access), `fr_rt` (refresh). Attributes: `HttpOnly; Secure; SameSite=Lax; Path=/`. |
| D2 | CSRF: double-submit cookie (`fr_csrf` non-HttpOnly readable by web) + `X-CSRF-Token` header verified on every state-changing request. Validation middleware skips GET/HEAD/OPTIONS and the `/auth/login` and `/auth/register` endpoints (they have no cookie yet). |
| D3 | Login/register/refresh set the cookies; logout clears them and revokes the refresh token. |
| D4 | Tests: missing token ⇒ 403, mismatched token ⇒ 403, valid pair ⇒ 200. |

### E · Rate limiting

| # | Task |
|---|---|
| E1 | ASP.NET `RateLimiter` middleware. |
| E2 | Policies: `auth-login` fixed-window 5/min/IP; `auth-register` fixed-window 3/min/IP; `auth-refresh` token-bucket 30/min/cookie+IP. |
| E3 | 429 response with `Retry-After`. Logged with correlationId, no PII. |
| E4 | Tests: 6th login attempt within window ⇒ 429. |

### F · Auth controllers (replace Phase 1 stubs)

| # | Task |
|---|---|
| F1 | `POST /auth/register` — validates email + password strength (NIST: ≥12 chars, not in compromised list — defer compromised list check to Phase 3); creates user; sets cookies; returns 201 + `{ user: { id, email, displayName } }`. |
| F2 | `POST /auth/login` — credential check, sets cookies. Generic error message on failure. Account lockout after 10 failed attempts in 15 min (in-memory store now, redis in Phase 5). |
| F3 | `POST /auth/refresh` — reads refresh cookie, rotates, sets new cookies. |
| F4 | `POST /auth/logout` — revokes refresh, clears cookies. |
| F5 | `GET /auth/me` — returns current user (JWT-authenticated). |
| F6 | Audit log entries (Serilog) for: register, login (success/fail), refresh-replay-detected, logout. No password or token in logs. |

### G · Teams + Players

| # | Task |
|---|---|
| G1 | `TeamsController` (auth required): `POST /teams`, `GET /teams` (own only), `GET /teams/{id}`, `PATCH /teams/{id}`, `DELETE /teams/{id}` (soft delete via `DeletedAt` — defer hard delete to Phase 6). |
| G2 | `PlayersController` (auth required, team-ownership check): `POST /teams/{teamId}/players`, `GET /teams/{teamId}/players`, `PATCH /teams/{teamId}/players/{id}`, `DELETE /teams/{teamId}/players/{id}`. |
| G3 | Authorisation policy `OwnsTeam` — resource-based `IAuthorizationHandler` reading `Team.OwnerUserId`. |
| G4 | Tests: cannot read someone else's team (403), cannot add player to someone else's team (403), validation errors (400). |

### H · Threat model

| # | Task |
|---|---|
| H1 | `docs/threat-model.md` — STRIDE table over the auth + teams + players surface, plus carry-overs from Phase 1 review (CORS AllowCredentials decision, CSP timing). |

### I · Validation & reviews

| # | Task |
|---|---|
| I1 | Run `dotnet test` — must be ≥30 tests green covering A–G. |
| I2 | Run `dotnet list package --vulnerable` — must be clean for newly added deps. |
| I3 | Security review (`security-engineer`) — focus on B/C/D/E and audit logging. |
| I4 | Principal review — DoD §3 + §10 + §11. Target ≥34/40. |

## Definition of Done (master prompt §3)

1. ✅ Plan exists (this file).
2. EF migration runs cleanly against postgres:16-alpine.
3. New tests cover register/login/refresh-rotation/refresh-replay/csrf/rate-limit/team-ownership.
4. `dotnet build` warning-free except for tracked OTel NU1902 carry-over.
5. No secret in code; `.env.example` updated with `Jwt:Key`, `ConnectionStrings:Postgres`.
6. Audit log lines for auth events; no password/token/raw-welfare in logs.
7. CI green.
8. Threat model committed.
9. Principal Reviewer approval.

## Iteration plan

- **Iter1:** A (data) + B (password) + C (tokens) + F (controllers) — get the 5 endpoints green end-to-end.
- **Iter2:** D (csrf cookies) + E (rate limiting) + G (teams + players) + H (threat model) + reviews.

Begin iter1.
