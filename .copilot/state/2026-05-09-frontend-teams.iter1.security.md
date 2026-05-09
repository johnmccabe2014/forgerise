# Iter1 — Security Review (Phase 5b: Frontend team setup + roster)

**Reviewer:** security agent
**Plan:** [2026-05-09-frontend-teams.md](./2026-05-09-frontend-teams.md)

## Surface

| Area | Change |
|---|---|
| pages | `/teams/new`, `/teams/[teamId]`. Dashboard CTAs now link to both. |
| components | `TeamCreateForm`, `PlayerAddForm`, `PlayerRow`, `slugify` helper. |
| tests | `slugify` (5), `TeamCreateForm` (3), `PlayerAddForm` (3). 25/25 web tests green. |

## Threats considered

| ID | Threat | Mitigation |
|---|---|---|
| S1 | Cross-team data exposure via shareable URL | Team detail RSC issues `serverFetchApi` with the user's cookies; API enforces `TeamScope.RequireOwnedTeam`. Non-owner gets 403/404 → page redirects to `/dashboard`. |
| S2 | XSS via player name / team name | All rendering goes through React. No `dangerouslySetInnerHTML` introduced. Error text is parsed JSON only and length-bounded (<200). |
| S3 | XSS via team `code` reflected in URL | `code` is API-validated `[A-Za-z0-9_-]+`. Client uses `slugify` which only emits `[a-z0-9-]`. URL composed via template literal, never `eval`/`href` from inline HTML. |
| S4 | CSRF on POST/DELETE | All requests use `/api/proxy/*` which auto-injects `X-CSRF-Token` from `fr_csrf` cookie; API CSRF middleware verifies. |
| S5 | Open-redirect from create-team flow | Redirect target is `/teams/${created.id}` from server response. `id` is a Guid; we don't accept user-controlled redirects. |
| S6 | Roster includes welfare data | API `PlayerDto` does not include welfare fields. Page renders only displayName/jersey/position. |
| S7 | Confirm-bypass on player delete | `confirm()` is a UX guard, not a security control. API still requires CSRF + ownership. |
| S8 | Verbose error leakage | `humaniseProblem` only renders `title` (≤200 chars) or a static fallback. No stack traces or raw bodies surfaced. |

## Findings

| Severity | ID | Finding | Disposition |
|---|---|---|---|
| Low | F1 | No CSP yet on Next side. | Tracked — add a strict CSP header in a later iter alongside server actions. |
| Info | F2 | `confirm()` for delete is browser-native; some assistive tech may surface awkwardly. | Acceptable for iter1 grassroots usage. |
| Info | F3 | `slugify` allows empty result (e.g. all-symbols name). Code field still required by `pattern`/server. | OK — UX guard via required+pattern. |

No must-fix items.

## Verdict

✅ Pass — proceed to principal review.
