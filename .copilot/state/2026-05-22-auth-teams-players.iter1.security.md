# Iter1 — Security Reviewer (Phase 2: Auth, Teams, Players)

**Reviewer:** security-reviewer agent
**Master prompt:** [.copilot/master.prompt.md](../master.prompt.md)
**Plan:** [2026-05-22-auth-teams-players.md](./2026-05-22-auth-teams-players.md)

---

## Scope

Phase 2 iter1 implements:

- Data model: `User`, `RefreshToken`, `Team`, `Player` with EF Core 9 + Npgsql; soft-delete query filters; unique constraints; design-time factory.
- Argon2id password hashing (`Auth/PasswordHasher.cs`) — Konscious 19456 KiB / 2 / 1.
- JWT issuance (HS256, 15 min) + refresh-token rotation with replay-chain revoke (`Auth/TokenService.cs`).
- Generic-error login + per-account lockout (`Auth/LoginLockout.cs`).
- AuthController endpoints (`/auth/register|login|refresh|logout|me`) with HttpOnly cookies + CSRF cookie mint.
- Cookie helper (`Auth/AuthCookies.cs`) — `fr_at` (path `/`), `fr_rt` (path `/auth`), `fr_csrf` (non-HttpOnly for double-submit).
- TeamsController + PlayersController with resource-based ownership checks.
- CSRF double-submit middleware + rate-limit policies (register 3/min, login 5/min, refresh 30/min).
- 31 passing tests covering hasher, token service, auth endpoints, teams, players, CSRF.

## STRIDE walk-through

See [docs/threat-model.md](../../docs/threat-model.md). All STRIDE rows have at least one Phase 2 mitigation.

## Findings

| Severity | ID | Finding | Resolution / Note |
|---|---|---|---|
| Med | S1 | Refresh-token cookie scoped to `/auth` so non-auth controllers can't read it. ✅ verified in `AuthCookies.SetTokens`. | OK |
| Med | S2 | Replay of revoked refresh token revokes the entire `ReplacedByTokenId` chain. ✅ unit + integration test (`Refresh_replay_revokes_session`). | OK |
| Med | S3 | Login error is generic `invalid_credentials`; lockout returns 423 only after threshold. ✅ test covers wrong password → 401. | OK |
| Med | S4 | CSRF middleware only fires on cookie-authenticated unsafe verbs. Bearer requests bypass — acceptable since bearer is not vulnerable to CSRF; documented in threat model. | OK |
| Med | S5 | Rate limiter partitions by `RemoteIpAddress`. Behind ingress this collides; flagged in threat-model as iter3 work. | Tracked |
| Low | S6 | Password min length is 12; no explicit complexity. Acceptable per NIST SP 800-63B; rely on length + lockout. | Tracked |
| Low | S7 | `Jwt:Key` enforced ≥32 chars only by length of the secret bytes; we check non-empty in non-Testing env. Add a 32-byte assert in iter2. | Tracked |
| Info | S8 | OTel exporter NU1902 carry-over from Phase 1 — out of scope this phase. | Deferred |

No High or Critical findings. Cleared for principal review.
