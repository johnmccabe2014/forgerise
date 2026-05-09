# Plan — ForgeRise Bootstrap (Phase 1: Foundation)

> Owner: Planner · Task ID: 2026-05-08-bootstrap · Date: 2026-05-08

## 0. Distilled understanding of the master prompt

ForgeRise = **Ops Intelligence for Coaches** (women's grassroots rugby first). Three layers:
1. Low-friction capture (attendance, notes, welfare flags, video)
2. AI interpretation (summaries, trends, workload, readiness)
3. Action generation (session plans, drills, match packs, welfare follow-ups)

Stack is locked: Next.js + TS + Tailwind, .NET 9 Web API, PostgreSQL, OTel, Docker, k3s, GitHub Actions, pnpm, GHCR. Auth = local email/password with Argon2/BCrypt + JWT + refresh rotation. Welfare data is privacy-critical; coaches see only safe categories (`Ready / Monitor / Modify Load / Recovery Focus`).

The MVP vertical slice (master prompt §8): **Coach registers → creates team → adds players → records attendance → writes session review → receives AI session plan.** That spans 6 backend modules (`auth`, `teams`, `players`, `attendance`, `sessions`, `ai-insights`) and 8 screens.

The master prompt also mandates production-mindedness from day 0: OTel, structured logs, secret hygiene, image scans, k3s deploy. So we cannot start with feature work — we need a foundation phase first.

## 1. Problem statement

We have a master prompt and a `.copilot/` operating system, but **no codebase**. We need a foundation that turns the rules into a working repo: monorepo skeleton, CI gates, container build, observability baseline, and an authenticated "hello" path — without any feature code yet. This unblocks the MVP vertical slice and proves every quality gate is enforceable.

## 2. Acceptance criteria (Phase 1)

- [ ] AC1: `pnpm -r build` and `dotnet build` succeed from a clean clone.
- [ ] AC2: `pnpm -r test` and `dotnet test` run, with at least one real passing test per project.
- [ ] AC3: CI workflow `ci.yml` runs lint + typecheck + tests for both web and api on PR + main and is green on the bootstrap PR.
- [ ] AC4: CI workflow `security.yml` runs dependency, secret, SAST, and Dockerfile scans; bootstrap PR has zero high/critical findings.
- [ ] AC5: CI workflow `docker-build.yml` builds web + api images, scans them, generates SBOM, and only pushes after scans pass.
- [ ] AC6: `deploy.yml` exists and is wired to the self-hosted runner targeting k3s, even if it deploys a placeholder; rollback documented.
- [ ] AC7: Auth contract is defined (registration + login + refresh) with request/response schemas; **no implementation yet**, just the OpenAPI shape and route stubs returning 501.
- [ ] AC8: OTel + structured JSON logging baseline emits a correlation-ID-tagged log line on every request from both web and api.
- [ ] AC9: `.env.example` files exist for web + api; no real secrets anywhere; `.gitignore` covers them.
- [ ] AC10: Three k3s namespaces declared in manifests (`forgerise-dev/staging/prod`); secret structure documented (no values committed).
- [ ] AC11: Brand tokens (colours from §6) materialised as Tailwind config + CSS variables; one minimal landing screen renders them.
- [ ] AC12: Welfare-safe-categories enum exists in shared types; lint rule (or test) prevents leaking raw welfare fields into log statements.

## 3. Non-goals (Phase 1)

- No real auth implementation (Phase 2).
- No real DB schema beyond migrations scaffolding (Phase 2).
- No AI calls, no provider abstraction implementation (Phase 3).
- No video upload (post-MVP).
- No production deployment — staging path only, gated behind manual approval.

## 4. Approach

**Chosen:** Monorepo with pnpm workspaces. `web/` (Next.js app), `api/` (.NET 9 solution), `infra/` (k8s manifests, Dockerfiles), `.github/workflows/`, `.copilot/` already in place. Foundation-first: skeletons + gates + observability before any feature. Each subsequent phase delivers one MVP vertical slice end-to-end.

**Rejected alternatives:**
- *Polyrepo (web + api + infra separate):* heavier coordination, slower for small team, harder to enforce shared types and consistent CI. Reject.
- *Start with the vertical slice and add CI/observability later:* contradicts master prompt §3 Definition of Done and §11/§12. Reject.
- *Helm chart from day 0:* premature; raw manifests are simpler until we have a second environment difference. Helm in Phase 4.

## 5. Roadmap (phases)

| Phase | Name | Goal | Exit criterion |
|------:|------|------|----------------|
| 1 | **Foundation** | Repo, CI, scans, OTel skeleton, brand tokens | All Phase-1 ACs green; bootstrap PR merged |
| 2 | **Auth + Teams + Players** | Real local auth, team CRUD, roster | Coach can register, log in, create team, add players |
| 3 | **Attendance + Sessions + Welfare-safe model** | Capture loop | Coach can take attendance, write session review; welfare categories computed safely |
| 4 | **AI Session Planner (vertical slice complete)** | Provider abstraction + first prompt + eval set | AI returns a structured next-session plan grounded in attendance + review |
| 5 | **Hardening + k3s deploy to staging** | Helm, ingress TLS, smoke tests, rollback drill | Deploy + rollback exercised on staging |
| 6 | **Match packs, drills, readiness trends** | Layer-2/3 expansion | Match-pack generation usable in a real session |

We ship **phase by phase**, each phase running through the full feedback loop.

## 6. Phase 1 work breakdown

| # | Task | Owner | Files | Test plan | AC |
|---|------|-------|-------|-----------|----|
| F1 | Monorepo scaffold | devops + frontend + backend | `pnpm-workspace.yaml`, `package.json`, `web/`, `api/ForgeRise.Api/`, `api/ForgeRise.sln`, `.editorconfig`, `.gitignore` | builds run | AC1 |
| F2 | Test scaffolding | tester | `web/vitest.config.ts`, `web/src/__tests__/smoke.test.ts`, `api/ForgeRise.Api.Tests/` (xUnit) | one passing test each | AC2 |
| F3 | CI workflow `ci.yml` | devops | `.github/workflows/ci.yml` | actionlint clean | AC3 |
| F4 | CI workflow `security.yml` | security + devops | `.github/workflows/security.yml` | dry run | AC4 |
| F5 | Dockerfiles + `docker-build.yml` + SBOM | devops | `web/Dockerfile`, `api/Dockerfile`, `.github/workflows/docker-build.yml` | hadolint, build local | AC5 |
| F6 | `deploy.yml` placeholder + rollback note | devops | `.github/workflows/deploy.yml`, `infra/README.md` | actionlint clean | AC6 |
| F7 | Auth contract (OpenAPI + 501 stubs) | backend | `api/ForgeRise.Api/Auth/*` controllers returning 501, schema docs | contract tests assert shape + 501 | AC7 |
| F8 | OTel + structured logging baseline | backend + frontend | `api/Program.cs` OTel wiring, `web/src/lib/logger.ts`, correlation-ID middleware | unit test for correlation-ID propagation | AC8 |
| F9 | `.env.example` + secret hygiene | security | `web/.env.example`, `api/.env.example`, `.gitignore` rules, `docs/secrets.md` | gitleaks scan clean | AC9 |
| F10 | k3s namespaces + secret structure docs | devops | `infra/k8s/namespaces.yaml`, `infra/k8s/secrets.example.yaml`, `infra/README.md` | kubeconform clean | AC10 |
| F11 | Brand tokens + landing page | frontend | `web/tailwind.config.ts`, `web/src/styles/tokens.css`, `web/src/app/page.tsx` | snapshot + a11y test | AC11 |
| F12 | Welfare-safe types + log-redaction lint | backend + security | `api/ForgeRise.Api/Welfare/SafeCategory.cs`, `api/.editorconfig` analyzer rule, `web/src/types/welfare.ts` | unit test refusing raw welfare in logs | AC12 |

12 tasks but tightly coupled into 4 commits: scaffold (F1, F2, F11), CI (F3-F6), service skeleton (F7, F8, F12), infra+secrets (F9, F10).

## 7. Risks & mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Self-hosted runner not available yet | High | Blocks F6 deploy test | Make `deploy.yml` `workflow_dispatch`-only with a `runs-on` placeholder + clear TODO; do not attempt real deploy in Phase 1 |
| OTel collector not available | Med | Logs/traces have no sink | Emit to stdout + file; collector wiring in Phase 5 |
| .NET 9 base image churn | Med | CVE noise | Pin by digest; weekly schedule in `security.yml` already covers re-scan |
| GHCR auth in CI before repo exists on GitHub | High | `docker-build.yml` push step fails | Gate push on `github.event_name != 'pull_request'` and `secrets.GHCR_TOKEN` presence |
| Adding too much in Phase 1 | High | Scope creep, slow loop | Strictly defer anything not in §2 ACs; Principal Reviewer enforces |
| Welfare-leak lint rule false positives | Low | Friction | Start as warning, promote to error after one phase of bake-in |

## 8. Validation strategy (Principal Reviewer)

For each task, Principal Reviewer checks:
1. AC mapping — every task ties to a numbered AC.
2. Tests exist and exercise behaviour, not types.
3. CI run on the bootstrap PR is green end-to-end.
4. `security.yml` reports zero new high/critical.
5. No secrets, real or placeholder-looking, in any file (gitleaks confirms).
6. Brand tokens render correctly (screenshot attached to PR).
7. Correlation ID flows web → api → log line in a manual smoke run.

## 9. Rollback plan

Phase 1 introduces no production surface. Rollback = `git revert` of the bootstrap PR. No data, no users, no deployed service yet.

## 10. Handoff

Next in the loop: this turn we execute **F1, F3, F6, F9, F10** (the lowest-risk, highest-leverage scaffolding) so the user has a runnable repo skeleton at end of iteration 1. F2, F4, F5, F7, F8, F11, F12 land in iteration 2 of Phase 1. Principal Reviewer scores at end of iteration 1 and sets the must-improve for iteration 2.
