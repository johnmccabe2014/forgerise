# Review — 2026-05-08-bootstrap · iteration 1

> Reviewer: Principal · Date: 2026-05-08

## Decision
- [ ] Approve
- [x] Approve with nits
- [ ] Request changes
- [ ] Reject & replan

Iteration 1 delivered the lowest-risk slice of Phase 1 (scaffold + CI + infra placeholders). No application code yet, by design. Approved to proceed to iter2; nits become must-improves.

## Scope verification (vs. plan)

| Plan item | Delivered? | Evidence |
|---|---|---|
| F1 monorepo scaffold | partial | `pnpm-workspace.yaml`, `package.json`, `web/`, `api/`, `.editorconfig`, `.gitignore` present; actual Next.js + .NET projects deferred to iter2 (documented in READMEs) |
| F3 ci.yml | yes | `.github/workflows/ci.yml` with web + api jobs, gracefully no-ops until projects land |
| F4 security.yml (partial) | yes | secret/dep/SAST/dockerfile scans wired |
| F5 docker-build.yml | yes | matrix build + Trivy + SBOM + gated push |
| F6 deploy.yml + rollback | yes | placeholder workflow + rollback in `infra/README.md` |
| F9 .env.example + secret hygiene | yes | both env examples + gitignore |
| F10 k8s namespaces + secret shape | yes | `infra/k8s/namespaces.yaml` + `secrets.example.yaml` |

Out of iter1 scope (planned for iter2): F2 tests, F7 auth contract, F8 OTel, F11 brand tokens + landing, F12 welfare-safe types + log lint.

## Scores (1–5)
| Dimension       | Score | Notes |
|-----------------|------:|-------|
| Correctness     | 4 | Scaffold matches plan; CI is graceful when projects are absent |
| Tests           | 1 | None yet — by design for iter1; **must address in iter2** |
| Security        | 4 | Pinned actions, least-privilege tokens, gitignore strong, no real secrets; two med recs deferred |
| Readability     | 5 | Self-documenting READMEs, comments where non-obvious |
| Design          | 4 | Foundation-first beats vertical-slice-first given §3/§11/§12 mandates |
| Performance     | n/a (3) | Nothing executes yet; scored neutral |
| Operability     | 3 | OTel not wired yet; rollback documented; deploy intentionally inert |
| Reversibility   | 5 | Whole iter1 is `git revert`-able; no live surface |
| **Total / 40**  | **29** | Healthy starting score |

## Must-fix (blocks iter2 → iter3 progression, not iter1 merge)
1. **F2 tests scaffolded** — at least one passing test in web (Vitest) and api (xUnit) so CI is genuinely green, not no-op green.
2. **F8 OTel + correlation-ID** — emit a structured JSON log line tagged with correlation ID on every request, both web and api. AC8.
3. **F12 welfare-safe types + log-redaction guard** — non-negotiable per master prompt §9, §11.
4. **F7 auth contract (501 stubs + OpenAPI)** — unblocks frontend work for Phase 2 without committing to implementation.
5. Carry-overs from Security review:
   - Add root-level `permissions: {}` to all workflows.
   - Add `.github/dependabot.yml` (npm + nuget + github-actions + docker).

## Nits (follow-up ok)
- `package.json` test script currently filters web only; expand once a tester runner lands at root for cross-stack `pnpm verify`.
- `docker-build.yml` Trivy version pinned at 0.30.0 — track upstream.
- `deploy.yml` references `vars.SELF_HOSTED_READY`; document this in `infra/README.md` alongside runner setup.

## Iteration delta
- **Better than last loop:** N/A (iteration 1).
- **Must improve next loop:** ship real tests (kills "Tests = 1") and OTel baseline (kills "Operability = 3"). Target: total ≥ 34/40 by end of iter2.

## Quality gates (iter1)
- [x] Plan acceptance criteria mapped (AC1, AC3-6, AC9, AC10 partial; rest queued for iter2)
- [n/a] Tests pass — no tests yet
- [x] Lint clean — no code to lint; YAML lints clean by inspection
- [x] Security scan clean — no findings ≥ med (two med *recommendations* logged, not findings)
- [x] No secrets committed
- [x] Reversible
