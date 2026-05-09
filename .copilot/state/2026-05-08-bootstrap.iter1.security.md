# Security review — iteration 1 (Phase 1 scaffold)

> Reviewer: Security agent · Date: 2026-05-08 · Task: 2026-05-08-bootstrap

Applied skill: `security-scan`. No code yet, so the review covers the scaffold itself: secrets handling, CI permissions, supply chain, infra surface.

## Findings

### [info] No high/critical findings in scaffold
Scaffold contains no application logic and no real secrets. Surface reviewed: `.gitignore`, `.env.example` files, `infra/k8s/*`, `.github/workflows/*`.

### [info] Gitignore covers env + secrets
`/.gitignore` blocks `.env`, `.env.*` (allow-listing `.env.example`), `*.pem|key|pfx|crt`, kubeconfigs, and any `infra/k8s/secrets.*.yaml` while explicitly allowing `secrets.example.yaml`. Acceptable.

### [low] `.env.example` placeholders are obviously fake
All sensitive fields use `CHANGE_ME` / `REPLACE_ME` / empty string. Confirmed no real-looking entropy that gitleaks could mistake for live keys. Acceptable.

### [info] CI workflows pin actions by SHA
All third-party actions in `ci.yml`, `security.yml`, `docker-build.yml`, `deploy.yml` are pinned by 40-char commit SHA with a version comment. Mitigates action-hijack risk. Continue this practice on every action update.

### [info] CI permissions follow least privilege
- `ci.yml`: `contents: read` only.
- `security.yml`: `contents: read`, `security-events: write` (needed for CodeQL).
- `docker-build.yml`: `contents: read`, `packages: write` (needed for GHCR).
- `deploy.yml`: `contents: read`.
No write tokens elsewhere. Concurrency groups present.

### [low] GHCR push gated correctly
`docker-build.yml` pushes only when `github.event_name != 'pull_request'` and only after Trivy passes (`exit-code: "1"`, `severity: HIGH,CRITICAL`). Forks cannot exfiltrate via PR.

### [low] `deploy.yml` cannot apply in Phase 1
Apply path explicitly aborts with an error if `inputs.confirm == 'DEPLOY'`. Self-hosted job gated behind a `vars.SELF_HOSTED_READY == 'true'` repo variable so it is a no-op until ops are ready. Good defence-in-depth for a scaffold.

### [med] **Recommendation for next iteration:** add `permissions: {}` at workflow root
Each job sets its own. Adding a top-level `permissions: {}` removes any inherited tokens by default. Defer to F4 expansion in iter2.

### [med] **Recommendation for next iteration:** enable Dependabot + secret-scanning push protection
Repo-level settings, not in scaffold yet. Add `.github/dependabot.yml` (npm + nuget + github-actions + docker) in iter2; turn on push protection in repo settings.

### [info] k3s namespaces declared with labels for environment isolation
`forgerise.io/environment` label set per namespace; supports NetworkPolicy and OPA targeting later.

### [info] Welfare/PII surface is zero in this iteration
No app code reads or writes welfare data yet. Welfare-leak lint rule (F12) is the first thing that ships in iter2 alongside the safe-category type.

## Verdict
**Pass** — no merge-blocking findings. Two `med` recommendations are scheduled for iteration 2 of Phase 1 (not blockers for iter1 because there is no app code to protect yet).

## Iter2 carry-over (must address)
1. Add root-level `permissions: {}` to all workflows.
2. Land `.github/dependabot.yml`.
3. Implement welfare-leak guard (F12) when the safe-category type lands.
4. Add CSP, HSTS, and security-headers middleware skeletons in api OTel commit (F8).
