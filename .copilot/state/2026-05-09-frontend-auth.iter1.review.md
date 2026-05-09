# Iter1 — Principal Reviewer (Phase 5: Frontend auth slice)

**Reviewer:** principal-reviewer
**Master prompt:** [.copilot/master.prompt.md](../master.prompt.md)
**Plan:** [2026-05-09-frontend-auth.md](./2026-05-09-frontend-auth.md)
**Security review:** [2026-05-09-frontend-auth.iter1.security.md](./2026-05-09-frontend-auth.iter1.security.md)

## Snapshot

- 9 new web tests (5 proxy unit + 4 AuthForm RTL). 14/14 web tests green. Backend still 64/64.
- `pnpm typecheck`, `pnpm lint`, `pnpm build` all clean.
- A coach can now navigate to `/register` → submit → land on `/dashboard` → sign out, all in a browser, end-to-end against the .NET API.

## Rubric (40 points)

| Area | Points | Score | Notes |
|---|---:|---:|---|
| Master-prompt alignment (§5 calm/mobile-first, §6 brand, §10 cookies+CSRF, §11 correlation) | 10 | 9 | Forms are mobile-first, brand tokens used, correlation id propagated through proxy. −1: no logout server action — used a client fetch. Acceptable for now. |
| Code quality / design | 8 | 7 | Proxy is small, single-purpose, and well-commented. Pages are server components where possible. −1: `serverApi` body type is `unknown` cast at the boundary; deferred until a typed client lands. |
| Tests | 8 | 8 | Proxy CSRF + Authorization-strip + Set-Cookie rewriting all covered. AuthForm covers happy path, server error, and client validation. |
| Security & welfare | 8 | 8 | Authorization stripped on inbound; CSRF preserved; no welfare data on this surface; no open-redirect; brand-friendly error messages don't reflect raw server text. |
| Operability & docs | 6 | 5 | Plan + security + review state files present. −1: README not updated with `pnpm dev` flow for the auth slice; track for iter2. |

**Total: 37/40 — APPROVED.**

## Risks / follow-ups (non-blocking)

- Add request body size cap to proxy before any file upload routes are exposed.
- Convert logout to a server action so the cookie clear and redirect happen in a single round-trip.
- Type the API responses end-to-end (generate from controller DTOs or hand-write a `client.ts`).
- Iter2: team-create form, players UI, attendance UI.

## Verdict

✅ APPROVED — Phase 5 iter1 complete. The first vertical slice is now usable from a browser.
