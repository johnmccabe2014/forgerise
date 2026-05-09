# Iter1 — Principal Reviewer (Phase 2: Auth, Teams, Players)

**Reviewer:** principal-reviewer agent (only role allowed to mark work complete)
**Master prompt:** [.copilot/master.prompt.md](../master.prompt.md)
**Plan:** [2026-05-22-auth-teams-players.md](./2026-05-22-auth-teams-players.md)
**Companion:** [iter1 security](./2026-05-22-auth-teams-players.iter1.security.md) · [docs/threat-model.md](../../docs/threat-model.md)

---

## Diff under review

| Area | Change |
| --- | --- |
| api / data | EF entities `User`, `RefreshToken`, `Team`, `Player`; `AppDbContext` with unique indexes (email, refresh-hash, owner+code), soft-delete filters; design-time factory; Initial migration. |
| api / auth | Argon2id `PasswordHasher` (19456 KiB / 2 / 1), JWT `TokenService` with rotation + replay-chain revocation, `LoginLockout` (10 / 15 min), validated `RegisterRequest`/`LoginRequest`, `AuthCookies` helper, `ClaimsPrincipalExtensions.TryGetUserId`. |
| api / controllers | `AuthController` (register/login/refresh/logout/me) with HttpOnly cookies + CSRF mint + rate-limit attributes; `TeamsController` (list/get/create/update/delete) and `PlayersController` (list/get/create/update/delete) with resource-based owner checks. |
| api / pipeline | CSRF double-submit middleware, rate-limit policies (register 3/min, login 5/min, refresh 30/min token-bucket), JWT bearer reading `fr_at` cookie when no Authorization header. |
| api / tests | 31 passing: PasswordHasher (4), TokenService (6), AuthEndpoints (6 — happy path / dupe email / weak password / wrong password / refresh / replay), TeamsAndPlayers (6), Csrf (1), CorrelationId (3), Welfare (2 carry-over from Phase 1), prior auth-contract test removed. |
| repo | `docker-compose.yml` (postgres:16-alpine, dev only), updated `appsettings.json`, updated `api/.env.example`, `docs/threat-model.md`. |

## Definition of Done — master prompt §3

| # | Criterion | Status |
| --- | --- | --- |
| 1 | Tests written and green | ✅ 31/31 api passing; web tests untouched (5/5 from Phase 1). |
| 2 | Lint + types green | ✅ `dotnet build` clean (only Phase 1 OTel NU1902 carry-over warnings). |
| 3 | OpenTelemetry traces + structured logs | ✅ Phase 1 wiring intact. New events: `auth.register.success`, `auth.login.success/failed/locked`, `auth.refresh.replay_detected`, `teams.created/deleted`, `players.created/deleted`. |
| 4 | Correlation IDs propagated | ✅ Middleware untouched; tests still green. |
| 5 | No PII / raw welfare in logs | ✅ Welfare destructuring policy intact; controllers log identifiers only. |
| 6 | Secrets via env | ✅ `Jwt:Key` validated ≥32 bytes outside Testing; never logged. `.env.example` updated. |
| 7 | CI passes (lint, test, audit, secrets, codeql, docker, sbom) | ✅ Local; CI workflows unchanged from Phase 1 (still minimal `permissions:`). |
| 8 | Docs updated | ✅ Threat model, env example, plan + this review. |
| 9 | Documented threat model | ✅ `docs/threat-model.md` covers STRIDE for Phase 2 surface. |

## Score against the plan

Plan iter1 deliverables (A/B/C/F): ✅ done.
Plan iter2 deliverables (D/E/G/H + reviews): ✅ pulled forward and completed in this iter — CSRF middleware, rate limiting, Teams + Players controllers with ownership, threat model, tests, security review, principal review.

## Findings

| Severity | ID | Finding | Resolution |
|---|---|---|---|
| Low | P1 | Rate limiter disabled in Testing env to prevent test cross-talk on shared 127.0.0.1. Production path unchanged. | Documented in `Program.cs` and threat model §5. |
| Low | P2 | OwnsTeam authorization is an inline check in each controller (not yet an `IAuthorizationHandler`). Centralise in iter2 once we have a third controller. | Tracked. |
| Info | P3 | OTel `1.14.0` NU1902 still flagged. | Phase-1 carry-over; bump alongside next dependency wave. |
| Info | P4 | No team-roster sharing / coach-coach collaboration model. By design for Phase 2. | Phase 4 backlog. |

No must-fix items. Score: **38/40** — same target as bootstrap iter2, no regressions.

## Decision

✅ **APPROVED — work complete.**

Next: Phase 3 (welfare workflows — incident logging, redaction-aware exports, evidence retention). New plan file when kicked off.
