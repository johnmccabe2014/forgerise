# Iter1 — Security Review (Phase 5c: Sessions list + Attendance UI)

**Reviewer:** security agent
**Plan:** [2026-05-09-frontend-attendance.md](./2026-05-09-frontend-attendance.md)

## Surface

| Area | Change |
|---|---|
| pages | `/teams/{teamId}/sessions/new`, `/teams/{teamId}/sessions/{sessionId}/attendance`. Team page now lists most recent 10 sessions. |
| components | `SessionCreateForm`, `AttendanceForm`, `sessionLabels` helper. |
| tests | `sessionLabels` (5), `SessionCreateForm` (3), `AttendanceForm` (4). 37/37 web tests green. |

## Threats considered

| ID | Threat | Mitigation |
|---|---|---|
| S1 | Cross-team attendance write via spoofed `playerId` | Server-side: `AttendanceController.Upsert` rebuilds the set of valid player IDs for `teamId` and rejects strangers with 400. Client never trusts input. |
| S2 | Cross-session write by URL tampering | Server `ResolveSession` enforces `teamId` ownership AND `sessionId ∈ team`. Page redirects to `/teams/{id}` on 401/403/404 — no error leakage. |
| S3 | CSRF on `POST /sessions` and bulk `PUT /attendance` | Routed via `/api/proxy/*` which auto-attaches `X-CSRF-Token` from `fr_csrf` cookie; backend CSRF middleware enforces. |
| S4 | XSS via player name / focus / location / note | All output through React. No `dangerouslySetInnerHTML`. Error messages parsed JSON only and length-bounded (<200 chars). |
| S5 | Welfare leakage | Attendance rows contain only `displayName`, `status`, optional coach-authored `note`, `recordedAt`. No wellness scores, no readiness category. `note` field is free-text under coach control — UX label says "Optional note" (not "wellness/medical note"). |
| S6 | Open redirect from session creation | Redirect target derived from API response `id` (Guid). Composed via template literal, never user-controlled string. |
| S7 | Date/time injection from client tz | `new Date(date+T+time).toISOString()` rejected when invalid via `Number.isNaN`. Server still validates `[Range(5,480)]` for duration. |
| S8 | Information disclosure on failure | `humaniseProblem` only reads `title`. No stack traces, request IDs, or upstream bodies surfaced verbatim. |

## Findings

| Severity | ID | Finding | Disposition |
|---|---|---|---|
| Low | F1 | Free-text `note` field could be used to record sensitive welfare info by an over-eager coach. | Acceptable for iter1 — coach-only field; placeholder is generic; `WelfareModule` redaction covers welfare-domain entities. Add explicit guidance copy in a later UX pass. |
| Info | F2 | Session list shows up to 10 most recent sessions; no pagination. | Tracked — paginate when teams accumulate >10 sessions. |
| Info | F3 | No CSP yet on Next side. | Same as iter2 — tracked for a dedicated security iter. |

No must-fix items.

## Verdict

✅ Pass — proceed to principal review.
