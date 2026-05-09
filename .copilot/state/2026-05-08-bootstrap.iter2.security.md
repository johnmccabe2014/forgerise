# Iter2 — Security Review (ForgeRise Bootstrap Phase 1)

**Reviewer:** security-engineer agent
**Master prompt:** [.copilot/master.prompt.md](../master.prompt.md)
**Plan:** [2026-05-08-bootstrap.md](./2026-05-08-bootstrap.md)
**Iter1 review:** [2026-05-08-bootstrap.iter1.review.md](./2026-05-08-bootstrap.iter1.review.md)

---

## Scope of changes since iter1

- Web: Next.js 14 scaffold, brand tokens, `ReadinessBadge`, welfare types, redaction lib, structured logger, correlation-ID middleware, Vitest + RTL.
- API: .NET 9 solution, `ForgeRise.Api` web API, `ForgeRise.Api.Tests` xUnit suite, Serilog JSON logging, OpenTelemetry tracing → OTLP, correlation-ID middleware, security headers middleware, welfare destructuring policy, auth controller stubs (501).
- Repo: `.github/dependabot.yml` with grouped updates for npm, nuget, github-actions, docker.

## Findings

### S1 · Welfare redaction has two tested layers — PASS
Both the Next.js `redactWelfare` walker (web/src/lib/welfare.ts) and the C# Serilog `WelfareDestructuringPolicy` (api/ForgeRise.Api/Welfare/WelfareDestructuringPolicy.cs) are exercised by tests that assert `[REDACTED]` replaces every name in `RAW_WELFARE_FIELDS` / `RawWelfareFields.Names`. Master prompt §9 / §11 satisfied for Phase 1.

### S2 · Correlation-ID input validation — PASS
Both middlewares enforce `8 ≤ len ≤ 128` and a `[A-Za-z0-9._-]` charset before echoing inbound headers. Tested in `CorrelationIdMiddlewareTests`. No header-injection vector.

### S3 · Auth surface returns 501 only — PASS
`AuthController` exposes register/login/refresh/logout, all returning 501 with `{ error: "not_implemented" }`. Contract test pins this for all four routes. Phase 2 must implement Argon2/JWT before any non-501 response.

### S4 · CORS lock-down — PASS (with carry-over for Phase 2)
`Cors:AllowedOrigins` is empty by default and configured per-environment via `appsettings*.json` / env. `AllowCredentials` removed because it conflicts with `AllowAnyHeader` until specific origin set is finalised. **Carry-over:** revisit before auth ships in Phase 2.

### S5 · Security headers middleware — PASS
`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy`, `Cross-Origin-Opener-Policy: same-origin` applied to every response. CSP intentionally deferred until web origin is finalised in Phase 5.

### S6 · Workflow permissions — PASS
All four workflows declare a top-level `permissions:` block scoped to the minimum required (`contents: read`, plus `packages: write` only for docker-build, `security-events: write` only for security). No `write-all` anywhere.

### S7 · Dependabot coverage — PASS
`.github/dependabot.yml` covers github-actions, npm (web), nuget (api), and docker (web/api), with grouped updates for `OpenTelemetry*`, `Serilog*`, `next`, and the testing stack so PRs stay reviewable.

### S8 · Carry-over: OpenTelemetry CVE noise — MEDIUM (open)
`dotnet list package --vulnerable --include-transitive` reports four moderate advisories
(GHSA-q834-8qmm-v933, GHSA-mr8r-92fq-pj8p, GHSA-4625-4j76-fww9, GHSA-g94r-2vxg-569j)
against `OpenTelemetry.Exporter.OpenTelemetryProtocol` and `OpenTelemetry.Api` 1.14.0.
These are advisories about telemetry-loss / DoS via malformed payloads, not RCE, and 1.14.0
is the latest stable as of bootstrap.

**Action for next iter:** lock the version with a comment in the `.csproj`, watch
[open-telemetry/opentelemetry-dotnet](https://github.com/open-telemetry/opentelemetry-dotnet) releases,
and bump as soon as a fixed minor lands. The `security.yml` workflow already runs
`dotnet list package --vulnerable` so any future regression will fail CI.

### S9 · Carry-over: pnpm install warns about deprecated transitives — LOW
Seven deprecated transitive packages (e.g. `glob@7`, `inflight@1`). Direct deps are current; this lives upstream in `eslint`/`next`. Dependabot will surface upgrades.

## Verdict

**PASS** for security carry-overs from iter1 — all four (S1, S2, S6, S7) are closed.
S8 stays open with a concrete monitoring plan.

Score: **9/10** (−1 for OTel CVE carry-over).
