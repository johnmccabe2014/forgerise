# Master Prompt — ForgeRise

> **Load order for every session:** this file → `config.yaml` → the relevant `agents/<role>.md` → any `skills/*.md` it references.

---

# 1. Mission

ForgeRise is a grassroots athlete development and coaching intelligence platform, initially focused on women’s grassroots rugby.

ForgeRise helps volunteer and pathway coaches save time, support player welfare, and make better training decisions from minimal manual input.

We optimise for:

1. correctness
2. security
3. testability
4. iteration speed

—in that order.

ForgeRise is not a generic club management app.

ForgeRise provides:

> Ops Intelligence for Coaches

by turning attendance, session notes, welfare signals, video metadata, and coaching observations into practical next actions such as:
- session plans
- drill recommendations
- readiness summaries
- welfare-aware training adjustments
- match packs
- operational coaching insights

---

# 2. Non-negotiable rules

1. **No code without a plan.**
   Every change starts as a numbered plan from the Planner agent.

2. **No code without tests.**
   New behaviour ships with automated tests in the same change.

3. **No merge without validation.**
   Validation = tests pass + lint clean + security scan clean + Principal Reviewer approval.

4. **Smallest viable change.**
   Prefer narrow, reversible diffs over sweeping refactors.

5. **No speculative abstraction.**
   Build for today’s requirement; refactor when a second use case appears.

6. **Surface uncertainty.**
   If confidence < 80%, stop and ask — don’t guess.

7. **Security is a first-class concern.**
   OWASP Top 10, no secrets in code, least privilege by default.

8. **Privacy by design.**
   Welfare and readiness data must be minimised, protected, and never exposed unnecessarily.

9. **No medical diagnosis.**
   ForgeRise may support readiness and welfare workflows, but must not diagnose medical conditions.

10. **Coach friction must stay low.**
    Every feature must save time or simplify decisions.

---

# 3. Definition of Done

A task is done when ALL are true:

- [ ] Plan exists and was followed or deviations documented
- [ ] Code compiles / type-checks
- [ ] New and existing tests pass locally and in CI
- [ ] Lint and formatter clean
- [ ] Security scan shows no new high/critical findings
- [ ] Container image scan shows no new high/critical findings
- [ ] No secrets, tokens, or sensitive welfare data logged
- [ ] OpenTelemetry/correlation logging added where relevant
- [ ] Principal Reviewer approved using `templates/review.md`

---

# 4. Stack & conventions

## Frontend
- Next.js
- TypeScript
- Tailwind CSS

## Backend
- .NET 9 Web API

## Database
- PostgreSQL

## Authentication
- Secure local authentication

## AI
- Provider abstraction supporting:
  - OpenAI
  - Azure OpenAI
  - future providers

## Observability
- OpenTelemetry
- Structured JSON logs
- Correlation IDs

## Infrastructure
- Docker
- k3s
- Kubernetes manifests or Helm

## CI/CD
- GitHub Actions

## Deployment
- Self-hosted GitHub runner targeting k3s

## Frontend testing
- Vitest
- React Testing Library

## Backend testing
- xUnit

## E2E / smoke testing
- Playwright
- lightweight API smoke tests

## Package manager
- pnpm

## Container registry
- GitHub Container Registry

## Branch naming
`type/short-description`

Examples:
- feat/player-readiness
- fix/auth-refresh
- sec/cors-hardening

## Commit format
- Conventional Commits

---

# 5. Product principles

ForgeRise must feel:

- modern
- calm
- empowering
- intelligent
- mobile-first
- grassroots authentic
- supportive rather than surveillance-based

Avoid:

- complex analytics dashboards
- hyper-masculine sports branding
- clinical medical styling
- AI hype
- admin-heavy workflows
- exposing sensitive welfare data to coaches

Every screen should answer:

1. What needs attention?
2. What changed?
3. What should I do next?
4. How does this save me time?

---

# 6. Brand system

## Product name
ForgeRise

## Positioning
> Ops Intelligence for Coaches

## Brand colours

### Primary
- Forge Navy: `#102A43`
- Rise Copper: `#C97B36`

### Supporting
- Soft Ember: `#E9A15B`
- Mist Grey: `#F4F7FA`
- Slate: `#486581`
- Deep Charcoal: `#1F2933`

## Readiness colours
- Ready: `#2F855A`
- Monitor: `#D69E2E`
- Modify Load: `#DD6B20`
- Recovery Focus: `#C53030`

## Typography
### Headings
- Inter Tight
- or Sora

### Body
- Inter

## UI Style
- clean cards
- rounded corners
- soft shadows
- large touch targets
- mobile-first layouts
- clear primary actions
- minimal clutter

---

# 7. Core architecture

ForgeRise is built around three layers.

## Layer 1 — Passive / Low-Friction Capture

- attendance
- availability
- short session reviews
- optional wellness check-ins
- simple injury/welfare flags
- optional video upload
- session context metadata
- match/training notes

## Layer 2 — AI Interpretation

- coaching summaries
- trend identification
- workload inference
- session insights
- tactical observations
- readiness trends
- highlight candidate generation

## Layer 3 — Action Generation

- AI-generated session plans
- drill recommendations
- modified workloads
- coach summaries
- player grouping suggestions
- welfare follow-up prompts
- match/training pack generation

---

# 8. MVP scope

## First vertical slice

Coach registers → creates team → adds players → records attendance → writes short session review → receives AI-generated next-session plan.

## Initial screens

- Login
- Register
- Dashboard
- Team Setup
- Player List
- Attendance
- Session Planner
- Session Review
- Welfare Check-In
- AI Session Generator
- Video Upload Placeholder
- Match Pack Generator

## Initial backend modules

- auth
- teams
- players
- attendance
- sessions
- welfare
- video
- ai-insights
- training-plans
- match-packs
- notifications
- audit

---

# 9. Welfare and readiness rules

- Athlete-owned wellness data remains private by default
- Coaches must not see raw sensitive wellness details unless explicitly permitted
- Coach-facing readiness uses safe categories only:
  - Ready
  - Monitor
  - Modify Load
  - Recovery Focus
- No medical diagnosis
- No menstrual or sensitive health data exposed directly to coaches
- All welfare access must be audited
- Design for GDPR-friendly consent, retention, and deletion
- Logs must never contain raw welfare data

---

# 10. Security requirements

- Local email/password auth initially
- Password hashing using Argon2 or BCrypt
- JWT access tokens
- Refresh token rotation
- HttpOnly secure cookies where appropriate
- CSRF protection where cookie auth is used
- Rate limiting for auth endpoints
- Account throttling or lockout
- Input validation on all API boundaries
- CORS locked down by environment
- Secure headers
- Least privilege service accounts
- Secrets stored in Kubernetes secrets or external secret providers
- No secrets committed
- No secrets baked into Docker images
- Audit logging for auth, welfare, and admin actions

---

# 11. Observability requirements

- OpenTelemetry from day 0
- Structured JSON logs
- Correlation ID per request
- Request metrics
- Error metrics
- Health endpoints
- Readiness endpoints
- Log redaction for sensitive fields
- No sensitive player, welfare, auth token, or password data in logs

---

# 12. Infrastructure and deployment

ForgeRise must be production-minded from day 0.

## Target deployment

- Docker containers
- k3s Kubernetes cluster
- GitHub Actions CI/CD
- Deployment through a self-hosted GitHub runner with access to k3s
- TLS ingress
- Environment-based configuration

## Kubernetes namespaces

- forgerise-dev
- forgerise-staging
- forgerise-prod

---

# 13. GitHub Actions requirements

Create workflows under `.github/workflows`.

## ci.yml

Runs on:
- pull requests
- main branch

Must include:
- checkout
- dependency restore
- frontend lint
- frontend typecheck
- frontend tests
- backend restore
- backend build
- backend tests

## security.yml

Runs on:
- pull requests
- main branch
- weekly schedule

Must include:
- dependency vulnerability scanning
- secret scanning
- container image scanning
- SAST/static analysis
- Dockerfile scanning

## docker-build.yml

Must:
- build web image
- build api image
- scan images
- generate SBOM
- push only after scans pass

## deploy.yml

Must:
- deploy to k3s
- validate rollout
- run smoke tests
- support rollback guidance

---

# 14. Agent routing summary

| Trigger | Owner |
|---|---|
| New requirement / ambiguity | planner |
| UI / client code | frontend |
| API / services / data | backend |
| Infrastructure / k3s / GitHub Actions / containers | devops |
| Auth / secrets / validation / security-sensitive work | security |
| Test creation / coverage / flakiness | tester |
| AI prompts / inference / recommendations | ai-engineer |
| Final approval before merge | principal-reviewer |

---

# 15. Feedback loop

```text
Planner ──► Builder(s) ──► Tester ──► Security ──► Principal Reviewer
   ▲                                                       │
   └─────────────── iterate on findings ◄──────────────────┘
```

---

# 16. When in doubt

Re-read this file.

If still unclear:
- ask the user
- never silently invent
- never trade security, privacy, or testability for speed

Build ForgeRise as if it could become a real production SaaS platform — not a throwaway prototype.
