# Phase 5 — iter2: Frontend team setup + roster

**Date:** 2026-05-09
**Owner:** planner → frontend → tester → security → principal-reviewer
**Master prompt:** [.copilot/master.prompt.md](../master.prompt.md)
**Builds on:** [2026-05-09-frontend-auth.md](./2026-05-09-frontend-auth.md)

## Goal

A coach who is already signed in can create a team, view its details, and add/remove players from a roster — all in the browser through the existing proxy.

## Scope

### A. Pages
- `/teams/new` — client form. POST `/teams` `{ name, code }`. Code field auto-derives a slug from Name; coach can edit. On 201 → `/teams/{id}`.
- `/teams/[teamId]` — RSC. Fetches `/teams/{id}` and `/teams/{id}/players`. On 403/404 → redirect `/dashboard`. Renders team header + roster section + add-player form.
- Dashboard CTA "Create your first team" links to `/teams/new`. Existing team rows link through to `/teams/{id}`.

### B. Components
- `TeamCreateForm.tsx` (client). Slugify on Name change unless coach has manually edited Code.
- `PlayerAddForm.tsx` (client). Inputs: displayName (required), jerseyNumber (0-999), position (free text ≤40). On success calls `router.refresh()`.
- `PlayerRow.tsx` (client). Delete button calls `DELETE /teams/{teamId}/players/{playerId}` then `router.refresh()`.

### C. Tests (Vitest + RTL)
- `slugify.test.ts` — covers diacritics, spaces, repeated separators, leading/trailing dashes, lowercase.
- `TeamCreateForm.test.tsx` — slug auto-fills then stops once user edits code; calls fetch with correct payload; shows server error.
- `PlayerAddForm.test.tsx` — clears inputs after success; surfaces 400 ProblemDetails title.

### D. Out of scope
- Edit team / edit player UIs (PUT endpoints).
- Attendance, sessions, welfare, plans UI (later iters).

### E. Security notes
- All requests go through `/api/proxy/*` so HttpOnly cookies + CSRF semantics are preserved.
- No raw welfare on this surface.
- Inputs validated client-side AND by the API; client validation is for UX only.
