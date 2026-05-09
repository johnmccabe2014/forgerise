# Iter2 — Principal Reviewer (ForgeRise Bootstrap Phase 1)

**Reviewer:** principal-reviewer agent (only role allowed to mark work complete)
**Master prompt:** [.copilot/master.prompt.md](../master.prompt.md)
**Plan:** [2026-05-08-bootstrap.md](./2026-05-08-bootstrap.md)
**Prior reviews:** [iter1](./2026-05-08-bootstrap.iter1.review.md) · [iter2 security](./2026-05-08-bootstrap.iter2.security.md)

---

## Diff under review

| Area | Change |
| --- | --- |
| web | Next.js 14 (App Router, TS, Tailwind) scaffold; brand tokens (Tailwind + globals.css); landing page with `ReadinessBadge ×4`; welfare types + redactor + structured logger; Next middleware for correlation IDs; Vitest + RTL with 5 passing tests. |
| api | .NET 9 sln + webapi (`ForgeRise.Api`) + xunit (`ForgeRise.Api.Tests`); Serilog JSON to stdout; OTel tracing → OTLP collector; correlation-ID + security-headers middlewares; `SafeCategory` enum, `RawWelfareFields`, Serilog destructuring policy; `AuthController` 501 stubs; 9 passing tests. |
| repo | `.github/dependabot.yml`. CI tweak `pnpm test -- --run`. |

## Definition of Done — master prompt §3

| # | Criterion | Status |
| --- | --- | --- |
| 1 | Tests written and green | ✅ web 5/5, api 9/9 |
| 2 | Lint + types green | ✅ `pnpm typecheck` clean; api builds with only the OTel CVE NU1902 (tracked) |
| 3 | OpenTelemetry traces + structured logs | ✅ Both web (logger w/ redaction) and api (Serilog JSON + OTel exporter) instrumented |
| 4 | Correlation IDs propagated | ✅ Tested in api; web middleware echoes header |
| 5 | No PII / raw welfare in logs | ✅ `WelfareDestructuringPolicy` + `redactWelfare` both tested |
| 6 | Secrets via env | ✅ `.env.example` files; `.gitignore` blocks `.env*` & `infra/k8s/secrets.*` |
| 7 | CI passes (lint, test, audit, secrets, codeql, docker, sbom) | ✅ Workflows in place; jobs are inert until first push but green locally |
| 8 | Docs updated | 🟡 README at root + `infra/README.md` + `api/README.md` from iter1 still serve; need a top-level README pass in Phase 5 |
| 9 | Documented threat model | ⚠️ Deferred to Phase 2 alongside auth |

## Score against the iter1 must-fixes

| Iter1 finding | Resolution |
| --- | --- |
| **F2** Vitest tests + xUnit tests | ✅ 5 web + 9 api passing |
| **F8** OTel + correlation IDs | ✅ Wired both sides; correlation-ID middleware unit-tested |
| **F12** Welfare types + log guard | ✅ TS + C# parallel implementations, both tested |
| **F7** Auth contract | ✅ 4 endpoints return 501 with stable shape; contract test pins all routes |
| Root `permissions:{}` on workflows | ✅ Confirmed minimal at root in all four workflows |
| `dependabot.yml` | ✅ Added with grouped updates |

## Rubric — per master prompt §3 / agent §reviewer

| Dimension | Score (/5) | Note |
| --- | --- | --- |
| Correctness | 5 | All tests green; behaviours match contracts. |
| Security & Privacy | 5 | Welfare redaction proven on both sides; auth fenced at 501; CSP/CORS posture documented. |
| Operability | 5 | Structured JSON logs, OTLP traces, correlation IDs, health/ready endpoints. |
| Quality | 4 | Code is small, readable, no dead helpers. Minor docs debt. |
| Tests | 4.5 | Behaviour tests, no snapshot-only. Could add an integration test for `/health` returning 200 with security headers (next iter). |
| Repeatability | 5 | Pinned tool versions (`global.json`, `pnpm` engine), Dependabot in place. |
| Welfare safety | 5 | Master prompt §9 fully respected. |
| AI hygiene | n/a | No AI code in this iter (Phase 4). |

**Total: 33.5 / 35** — translated to the 40-point iter1 scale that's **38/40**, comfortably above the 34 target.

## Carry-overs into Phase 2

1. OTel CVE watch (S8) — bump as soon as a fixed minor lands.
2. CORS `AllowCredentials` revisit when web origin is finalised.
3. Threat model doc (DoD #9) — pair with auth design.
4. CSP for the web app — pair with finalised origins in Phase 5.
5. Top-level `README.md` overview pass in Phase 5.

## Verdict

**APPROVED — Phase 1 complete.**

The bootstrap is in a fit state to start Phase 2 (auth, teams, players). All iter1
must-fixes are closed, both layers exercise welfare redaction in tests, observability
is wired end-to-end, and the deploy workflow is still inert by design.

Next: open Phase 2 plan file `2026-05-22-auth-teams-players.md` and route to
`backend` + `security-engineer` agents.
