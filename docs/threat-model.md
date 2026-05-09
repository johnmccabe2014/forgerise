# ForgeRise — Phase 2 Threat Model (Auth, Teams, Players)

> Scope: Phase 2 increment. Aligns with `master.prompt.md §10 Security` and `agents/security-reviewer.md`.

## 1. Assets

| Asset | Sensitivity | Notes |
|---|---|---|
| User credentials (password) | High | Never logged; Argon2id-hashed at rest. |
| Refresh token (cookie) | High | HttpOnly, scoped to `/auth`, hashed in DB. |
| Access token (JWT) | Medium | 15 min lifetime, HttpOnly cookie. |
| Personal data (email, display name) | Medium | Welfare-redactor trims accidental PII in logs. |
| Player welfare notes (Phase 3+) | High | Out of scope this phase but DB schema reserves `DeletedAt` for retraction. |

## 2. Trust boundaries

```
Browser ──TLS──▶ Reverse proxy / k3s ingress ──▶ ForgeRise.Api ──▶ Postgres
                                              └─▶ OpenTelemetry collector
```

Untrusted: browser, public network. Trusted: cluster-internal traffic (still mTLS where available; not enforced this phase).

## 3. STRIDE

| Threat | Mitigation (Phase 2) | Residual |
|---|---|---|
| **S**poofing — credential stuffing | Argon2id (m=19456, t=2), 10/15-min lockout, login rate-limit 5/min/IP. | Distributed botnets bypass IP limit; track in iter3 (CAPTCHA / device fingerprint). |
| **S** — session hijack via XSS | Access + refresh cookies are HttpOnly; CSP planned in iter3; SameSite=Lax; strict CORS allow-list. | XSS still possible through future user-generated content; sanitisation pending. |
| **T**ampering — JWT forgery | HS256 with ≥32-char key from secret store; ValidateIssuer/Audience/Lifetime/Key enforced; ClockSkew=30s. | Key rotation procedure not yet documented. |
| **T** — refresh-token theft | Refresh stored as SHA-256 hex; rotation on use; replay (use of revoked token) revokes the entire chain and forces re-login. | Stolen-but-not-used token still valid until expiry/rotation. |
| **R**epudiation — disputed actions | Structured `auth.*` and `teams.*`, `players.*` events with correlation-id; immutable append-only log target in iter3. | Logs are not signed. |
| **I**nformation disclosure — PII in logs | `WelfareDestructuringPolicy` redacts welfare/PII fields; password never serialised; login error is generic `invalid_credentials`. | Stack traces in dev mode may leak schema; production runs with `Production` env. |
| **I** — IDOR on `/teams/{id}` | Every request resolves user from `sub` claim and verifies `team.OwnerUserId == userId`; non-owner gets 403. Players inherit team ownership check. | No team-level RBAC yet (single-owner model). |
| **D**enial of service — auth flood | Rate limits: register 3/min, login 5/min, refresh 30/min token-bucket — all per remote IP. | NAT users behind a single IP collide; tune via `X-Forwarded-For` trust list in iter3. |
| **D** — accidental DB wipe | Soft-delete on Team and Player via `DeletedAt`; query filters hide deleted rows. EF migrations checked into `Data/Migrations`. | No automated backup test. |
| **E**levation of privilege — missing authorize | All non-auth controllers carry `[Authorize]`; endpoint scan covered by integration tests asserting 401/403. | New controllers must follow the pattern; static scan recommended. |
| CSRF on cookie-authenticated endpoints | Double-submit: `fr_csrf` non-HttpOnly cookie + `X-CSRF-Token` header validated by `CsrfMiddleware`; enforced for all unsafe verbs except `/auth/*` (which mint the cookie). Bearer requests are exempt. | Token rotation only on full session churn; consider per-request rotation for high-value mutations later. |

## 4. Security controls implemented this phase

- Argon2id password hashing (`Auth/PasswordHasher.cs`) — Konscious 19456 KiB, 2 iters, 1 lane.
- Generic login error + per-account lockout (`Auth/LoginLockout.cs`).
- JWT validation with strict parameters; access token also accepted from `fr_at` cookie via `JwtBearerEvents.OnMessageReceived`.
- Refresh-token rotation with replay-chain revocation (`Auth/TokenService.cs`).
- HttpOnly, SameSite=Lax cookies (`fr_at`, `fr_rt`, `fr_csrf`); `Secure` flag on for non-Development/Testing.
- CSRF double-submit middleware (`Observability/CsrfMiddleware.cs`).
- Rate limiting on auth endpoints (`Program.cs`).
- Resource-based authorization in Teams & Players controllers (owner check on every mutation and read).
- EF Core query filters for soft-deleted rows; design-time factory keeps migrations CI-friendly.
- Welfare-aware structured logging carried over from Phase 1.

## 5. Open items / iter3 backlog

- CSP header + nonce strategy, Permissions-Policy review.
- Key-rotation runbook for `Jwt:Key`.
- Centralised audit log target (immutable / append-only).
- CAPTCHA / proof-of-work on `/auth/register` for distributed attack scenarios.
- Trust-list for `X-Forwarded-For` so rate limits are accurate behind ingress.
- Add OWASP ZAP baseline scan to `security.yml` (currently runs CodeQL + dependency review only).
