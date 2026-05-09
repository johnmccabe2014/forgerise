# Iter1 — Principal Reviewer (Phase 5b: Frontend team setup + roster)

**Plan:** [2026-05-09-frontend-teams.md](./2026-05-09-frontend-teams.md)
**Security:** [2026-05-09-frontend-teams.iter1.security.md](./2026-05-09-frontend-teams.iter1.security.md)

## Snapshot

- 11 new web tests (slugify 5, TeamCreateForm 3, PlayerAddForm 3). 25/25 web tests pass; backend 64/64 unchanged.
- Lint, typecheck, and `next build` all clean. New routes `/teams/new` and `/teams/[teamId]` ship as expected (`teams/new` static, `teams/[teamId]` dynamic).
- A coach can now register → log in → dashboard → create a team → land on the team page → add and remove players, all in a browser.

## Rubric (40 points)

| Area | Points | Score | Notes |
|---|---:|---:|---|
| Master-prompt alignment (§5/§6 calm UI, §10 cookie+CSRF preserved, §8 first slice progress) | 10 | 9 | Pages are mobile-first and brand-aligned. Slug helper makes "the obvious thing" automatic for coaches. −1: still no edit-team flow. |
| Code quality / design | 8 | 7 | Components are small and single-purpose. Slug helper isolated and tested. −1: `humaniseProblem` is duplicated between forms — fine for iter1, candidate for shared util when a third form lands. |
| Tests | 8 | 8 | Slugify edge cases (diacritics, repeated separators, length cap) covered. Forms cover happy path, server error, and client validation. |
| Security & welfare | 8 | 8 | Ownership enforced server-side; redirect on 403. No welfare on this surface. CSRF preserved through proxy. |
| Operability & docs | 6 | 5 | Plan + security + review state files present. −1: still no README walkthrough; deferred. |

**Total: 37/40 — APPROVED.**

## Risks / follow-ups

- Add an `EditTeam` form (PUT `/teams/{id}` already exists).
- Extract a shared `humaniseProblem` util when a third form needs it.
- Add a strict Content-Security-Policy on the Next side before the next phase.
- Iter3: attendance UI for a chosen session.

## Verdict

✅ APPROVED — Phase 5 iter2 complete. Roster management is now fully browser-driven.
