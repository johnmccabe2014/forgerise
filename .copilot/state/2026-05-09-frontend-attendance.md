# Plan — Phase 5 iter3: Sessions list + Attendance UI

**Date:** 2026-05-09
**Owner:** planner → frontend
**Goal:** Bring the first vertical slice (master prompt §8) one step further by letting a coach schedule a training session and record attendance, all in the browser. Closes the gap between roster (iter2) and AI session-plan generation (already in backend).

## Scope

A coach can:

1. From a team page, see upcoming/recent sessions and click "+ New session".
2. On `/teams/{teamId}/sessions/new`, schedule a session (date+time, duration, type, location, focus).
3. From a session row, click through to `/teams/{teamId}/sessions/{sessionId}/attendance`.
4. Mark each player **Present / Late / Excused / Absent** with an optional short note, then save in one batch (PUT bulk upsert).

Out of scope (later iters): post-session review notes UI, AI session-plan UI, edit/delete sessions UI.

## Backend touchpoints (already implemented, no API changes)

- `GET  /teams/{teamId}/sessions` → `SessionDto[]`
- `POST /teams/{teamId}/sessions` → `SessionDto` (Created)
- `GET  /teams/{teamId}/sessions/{id}` → `SessionDto`
- `GET  /teams/{teamId}/sessions/{sessionId}/attendance` → `AttendanceRowDto[]`
- `PUT  /teams/{teamId}/sessions/{sessionId}/attendance` → `AttendanceRowDto[]`

Enums (numeric on the wire):
- `SessionType`: Training=0, Match=1, Other=2
- `AttendanceStatus`: Absent=0, Present=1, Late=2, Excused=3

## Files

### A — Pages
1. `web/src/app/teams/[teamId]/sessions/new/page.tsx` — wraps `<SessionCreateForm/>`.
2. `web/src/app/teams/[teamId]/sessions/[sessionId]/attendance/page.tsx` — RSC: fetch session + roster rows; render `<AttendanceForm/>`. Redirect to team page on 401/403.
3. **Update** `web/src/app/teams/[teamId]/page.tsx` — add "Sessions" section listing the most recent 10 sessions with link to attendance + "+ New session" CTA.

### B — Components
1. `web/src/components/SessionCreateForm.tsx` — client form. Splits date + time inputs to keep mobile UX simple, then composes ISO string. Submits `{ scheduledAt, durationMinutes, type, location?, focus? }` to `/api/proxy/teams/{teamId}/sessions`. On 201 → `router.replace(`/teams/{teamId}/sessions/{newId}/attendance`)`.
2. `web/src/components/AttendanceForm.tsx` — client form. Receives `teamId`, `sessionId`, and initial rows. Local state per player (status enum + note). On submit: PUT to `/api/proxy/teams/{teamId}/sessions/{sessionId}/attendance`. Shows transient "Saved" confirmation; updates local state from response.
3. `web/src/lib/sessionLabels.ts` — small helpers `sessionTypeLabel(n)` and `attendanceStatusLabel(n)` for human strings.

### C — Tests
1. `web/src/lib/sessionLabels.test.ts` — labels for each enum value + fallback.
2. `web/src/components/SessionCreateForm.test.tsx`:
   - composes ISO datetime from inputs and POSTs the right payload, then redirects to attendance page.
   - surfaces 400 ProblemDetails title.
3. `web/src/components/AttendanceForm.test.tsx`:
   - renders one row per player with current status preselected.
   - changing a status and submitting PUTs the bulk payload with all rows.
   - shows "Saved" feedback on 200 and refreshed timestamps from response.

## Validation

- `pnpm typecheck`, `pnpm lint`, `pnpm vitest run`, `pnpm build` all green.
- Backend untouched → 64/64 unchanged.

## Acceptance

- Brand-aligned, calm, mobile-first UI (cards + soft shadow, big touch targets for attendance).
- No welfare data on this surface (attendance is logistical only — `note` is coach-authored, ≤500 chars, free text).
- All mutations go through `/api/proxy/*` so CSRF token is auto-attached.
- Vertical slice now: register → login → dashboard → create team → add players → schedule session → record attendance.
