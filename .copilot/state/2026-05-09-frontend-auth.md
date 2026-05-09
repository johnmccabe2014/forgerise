# Phase 5 — iter1: Frontend auth slice

**Date:** 2026-05-09
**Owner:** planner → frontend → security → principal-reviewer
**Master prompt:** [.copilot/master.prompt.md](../master.prompt.md)

## Goal

Make the first vertical slice usable in a browser: a coach can register, log in, and see their teams from the Next.js app. Subsequent iter2/3 phases will add team setup, attendance, sessions UI.

## Sections

### A. API client / proxy
- Catch-all Next route handler `app/api/proxy/[...path]/route.ts` forwards browser → .NET API.
- Rewrites upstream `Set-Cookie` to drop `Domain=` and remap `Path=/auth` → `Path=/api/proxy/auth` so SameSite=Lax cookies stick to Next origin.
- Auto-injects `X-CSRF-Token` header from the `fr_csrf` cookie for state-changing verbs.
- Forwards `x-correlation-id` from middleware so the API logs use the same id.
- Server-side helper `serverFetchApi(path)` for RSC pages (forwards cookies via `next/headers`).

### B. Pages
- `/login` — client component form, posts to `/api/proxy/auth/login`. Errors surfaced inline. On 200, `router.replace('/dashboard')`.
- `/register` — same shape, posts to `/api/proxy/auth/register`.
- `/dashboard` — RSC. Calls `/auth/me` + `/teams`. On 401 redirects to `/login`. Renders coach name + team list. Empty state with CTA "Create your first team" (placeholder for iter2).
- `/logout` — server action that POSTs `/auth/logout` and redirects.

### C. Tests (Vitest + RTL)
- `apiProxy.test.ts` — Set-Cookie rewriting (drops Domain; `Path=/auth` → `Path=/api/proxy/auth`), CSRF header injection from cookie.
- `LoginForm.test.tsx` — submits values, surfaces server error, calls router.replace on 200.
- `RegisterForm.test.tsx` — same shape; password too short → inline error.

### D. Security review
Sentinel checks: no auth tokens or welfare data ever rendered into HTML; proxy never forwards `Authorization` headers from browser; CSRF still enforced.

### E. Out of scope (iter2+)
- Team-create UI, player UI, attendance UI, session UI, welfare UI, plan UI, realtime updates, password reset, account settings.
