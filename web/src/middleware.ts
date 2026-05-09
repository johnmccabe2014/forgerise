import { NextResponse, type NextRequest } from "next/server";

/**
 * Correlation-ID propagation. Master prompt §11.
 * - Read inbound `x-correlation-id` if present.
 * - Otherwise mint a UUID.
 * - Echo on response so the browser dev-tools shows it; downstream API
 *   handlers should forward it to the .NET API on every fetch.
 */
export function middleware(req: NextRequest) {
  const incoming = req.headers.get("x-correlation-id");
  const correlationId =
    incoming && /^[A-Za-z0-9._-]{8,128}$/.test(incoming)
      ? incoming
      : crypto.randomUUID();

  const requestHeaders = new Headers(req.headers);
  requestHeaders.set("x-correlation-id", correlationId);

  const res = NextResponse.next({ request: { headers: requestHeaders } });
  res.headers.set("x-correlation-id", correlationId);

  // Security headers (master prompt §10). Tight CSP — no inline scripts,
  // no eval, default deny. `connect-src` covers the same-origin /api/proxy
  // pass-through; if we ever talk to the API directly from the browser the
  // host needs to be added here.
  const isProd = process.env.NODE_ENV === "production";
  const csp = [
    "default-src 'self'",
    // Next.js inlines a small bootstrap script in the document; keep
    // 'unsafe-inline' for *styles* only (Tailwind/inline style attrs) and
    // rely on Next's nonce (or strict hash on prod) for scripts.
    isProd
      ? "script-src 'self'"
      : "script-src 'self' 'unsafe-eval' 'unsafe-inline'",
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: blob:",
    "font-src 'self' data:",
    "connect-src 'self'",
    "frame-ancestors 'none'",
    "form-action 'self'",
    "base-uri 'self'",
    "object-src 'none'",
    isProd ? "upgrade-insecure-requests" : "",
  ]
    .filter(Boolean)
    .join("; ");

  res.headers.set("Content-Security-Policy", csp);
  res.headers.set("X-Frame-Options", "DENY");
  res.headers.set("X-Content-Type-Options", "nosniff");
  res.headers.set("Referrer-Policy", "strict-origin-when-cross-origin");
  res.headers.set("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
  if (isProd) {
    res.headers.set(
      "Strict-Transport-Security",
      "max-age=31536000; includeSubDomains",
    );
  }
  return res;
}

export const config = {
  matcher: "/((?!_next/static|_next/image|favicon.ico).*)",
};
