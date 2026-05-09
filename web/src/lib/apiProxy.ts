/**
 * Browser → Next → API proxy.
 *
 * Why a proxy?
 * - The .NET API sets HttpOnly auth cookies (`fr_at`, `fr_rt`) plus a JS-readable
 *   CSRF cookie (`fr_csrf`) with SameSite=Lax. These cookies only stick to the
 *   API origin. Routing browser traffic through the Next origin lets us keep
 *   the same cookies in a single-origin browser session without weakening
 *   SameSite or enabling cross-origin credentials.
 *
 * Behaviour:
 * - Forwards method, body, and selected headers to `${API_BASE}/<path>`.
 * - Strips `Authorization` from the inbound request — clients must use cookies.
 * - Forwards inbound cookies (so the API sees `fr_at` / `fr_csrf` / `fr_rt`).
 * - For state-changing methods, injects `X-CSRF-Token` from the `fr_csrf`
 *   cookie so the API's CSRF middleware sees the double-submit value.
 * - Rewrites upstream `Set-Cookie` headers to drop `Domain=` and remap
 *   `Path=/auth` → `Path=/api/proxy/auth` so the refresh cookie path-scopes
 *   correctly behind the proxy.
 * - Forwards `x-correlation-id` so API logs share the browser's request id.
 *
 * This module is server-only.
 */
import { NextResponse, type NextRequest } from "next/server";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080";
const PROXY_PREFIX = "/api/proxy";

const FORWARD_REQ_HEADERS = new Set([
  "accept",
  "accept-language",
  "content-type",
  "cookie",
  "x-correlation-id",
  "user-agent",
]);

const STATE_CHANGING = new Set(["POST", "PUT", "PATCH", "DELETE"]);

export async function proxyToApi(
  req: NextRequest,
  pathParts: string[] | undefined,
): Promise<NextResponse> {
  const path = "/" + (pathParts ?? []).join("/");
  const url = new URL(API_BASE + path);
  req.nextUrl.searchParams.forEach((v, k) => {
    url.searchParams.append(k, v);
  });

  const headers = new Headers();
  req.headers.forEach((v, k) => {
    if (FORWARD_REQ_HEADERS.has(k.toLowerCase())) headers.set(k, v);
  });
  // Browser MUST authenticate via cookies; never trust an inbound Authorization.
  headers.delete("authorization");

  if (STATE_CHANGING.has(req.method)) {
    const csrf = readCookie(req.headers.get("cookie"), "fr_csrf");
    if (csrf) headers.set("X-CSRF-Token", csrf);
  }

  const init: RequestInit = {
    method: req.method,
    headers,
    redirect: "manual",
  };
  if (STATE_CHANGING.has(req.method)) {
    const buf = await req.arrayBuffer();
    if (buf.byteLength > 0) init.body = buf;
  }

  let upstream: Response;
  try {
    upstream = await fetch(url.toString(), init);
  } catch {
    return NextResponse.json(
      { error: "Upstream unavailable" },
      { status: 502 },
    );
  }

  const resHeaders = new Headers();
  upstream.headers.forEach((v, k) => {
    const lk = k.toLowerCase();
    if (lk === "set-cookie" || lk === "transfer-encoding") return;
    resHeaders.append(k, v);
  });

  // Set-Cookie may be split into multiple values; preserve them all.
  const rawSetCookie = collectSetCookies(upstream.headers);
  for (const sc of rawSetCookie) {
    resHeaders.append("set-cookie", rewriteSetCookie(sc));
  }

  const body = await upstream.arrayBuffer();
  return new NextResponse(body, {
    status: upstream.status,
    headers: resHeaders,
  });
}

export function rewriteSetCookie(value: string): string {
  // Drop any Domain= attribute (case-insensitive).
  let out = value.replace(/;\s*Domain=[^;]*/gi, "");
  // Remap Path=/auth (exact path the API uses for fr_rt) to the proxied path.
  out = out.replace(/(;\s*Path=)\/auth(\b)/gi, `$1${PROXY_PREFIX}/auth$2`);
  return out;
}

function readCookie(cookieHeader: string | null, name: string): string | null {
  if (!cookieHeader) return null;
  for (const part of cookieHeader.split(";")) {
    const [k, ...rest] = part.trim().split("=");
    if (k === name) return decodeURIComponent(rest.join("="));
  }
  return null;
}

function collectSetCookies(headers: Headers): string[] {
  // Headers.getSetCookie() exists in undici/Node 18+; fall back to .get() if needed.
  const anyHeaders = headers as unknown as { getSetCookie?: () => string[] };
  if (typeof anyHeaders.getSetCookie === "function") {
    return anyHeaders.getSetCookie();
  }
  const single = headers.get("set-cookie");
  return single ? [single] : [];
}
