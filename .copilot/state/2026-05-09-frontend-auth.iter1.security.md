# Iter1 — Security Review (Phase 5: Frontend auth slice)

**Reviewer:** security agent
**Master prompt:** [.copilot/master.prompt.md](../master.prompt.md)
**Plan:** [2026-05-09-frontend-auth.md](./2026-05-09-frontend-auth.md)

## Surface under review

| Area | Change |
| --- | --- |
| web / proxy | `src/lib/apiProxy.ts` + catch-all `app/api/proxy/[...path]/route.ts`. |
| web / RSC fetch | `src/lib/serverApi.ts` forwards cookies + correlation id from `next/headers`. |
| web / pages | `/login`, `/register`, `/dashboard`, logout button. |
| web / tests | Proxy unit tests (CSRF injection, Authorization stripping, Set-Cookie rewriting), AuthForm RTL tests (login/register success + 401 + short-password block). |

## Threats considered

| ID | Threat | Mitigation |
|---|---|---|
| S1 | Browser-injected `Authorization: Bearer …` reaches API and bypasses cookie auth path | Proxy explicitly `headers.delete("authorization")` before forwarding. Covered by test `injects X-CSRF-Token … forwards correlation id … Authorization … MUST be stripped`. |
| S2 | CSRF: cookie-bearing POST without matching header | Proxy reads `fr_csrf` from inbound cookies and sets `X-CSRF-Token` on state-changing methods. API CSRF middleware still enforces — proxy can't forge a token a browser doesn't already hold. |
| S3 | Cookie origin mismatch leaks tokens to other sites | Browser only ever talks to Next origin. Set-Cookie `Domain=` is stripped, so cookies bind to Next host only. SameSite=Lax preserved. |
| S4 | Refresh-token cookie path scoping breaks behind proxy | `Path=/auth` is rewritten to `Path=/api/proxy/auth`, matching the proxied refresh endpoint. Test asserts the rewrite. |
| S5 | XSS via reflected error text in form alert | `humaniseAuthError` rejects payloads >200 chars and only renders parsed `title`. React escapes by default. No `dangerouslySetInnerHTML`. |
| S6 | Auth tokens or welfare data in HTML/log output | Dashboard reads only `displayName`, `id`, `email`, and team `name`/`code`. No raw welfare fields ever fetched here; welfare slice is on its own endpoints not used by iter1. Proxy never logs body. |
| S7 | Open-redirect via login | Login always navigates to fixed `/dashboard`. No `?returnTo=` parameter. |
| S8 | Catch-all proxy abuse for SSRF | Path is appended to fixed `API_BASE` (env). No host override possible. Inbound query string forwarded but base host is fixed. |
| S9 | Browser bypasses CSRF by setting its own `X-CSRF-Token` | API still enforces match against `fr_csrf` cookie. Browser-set headers don't change the cookie value, so a forged header without matching cookie fails server-side. |

## Findings

| Severity | ID | Finding | Disposition |
|---|---|---|---|
| Low | F1 | Proxy does not enforce a request body size cap. | Tracked. Add 1 MB cap before exposing welfare/file upload endpoints through it. |
| Low | F2 | `serverFetchApi` returns parsed body untyped (`unknown` cast). | Acceptable for iter1; pages narrow the type at the call site. |
| Info | F3 | Logout button discards refresh-token cookie via API but does not block the click on network failure. | Acceptable — UI redirects regardless and stale cookies expire server-side. |
| Info | F4 | Proxy currently allows GET on any API path (e.g. `/auth/me`). | Intentional — API still authorises per endpoint. |

No must-fix items.

## Verdict

✅ Pass — proceed to principal review.
