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

  // Per-request CSP nonce. Next.js auto-applies this to its inlined
  // bootstrap and route scripts when it sees the `x-nonce` request header,
  // so we can keep `script-src` strict (no 'unsafe-inline') in prod.
  const isProd = process.env.NODE_ENV === "production";
  const nonceBytes = new Uint8Array(16);
  crypto.getRandomValues(nonceBytes);
  const nonce = btoa(String.fromCharCode(...nonceBytes));
  requestHeaders.set("x-nonce", nonce);

  const res = NextResponse.next({ request: { headers: requestHeaders } });
  res.headers.set("x-correlation-id", correlationId);

  // Security headers (master prompt §10).
  const csp = [
    "default-src 'self'",
    isProd
      ? `script-src 'self' 'nonce-${nonce}' 'strict-dynamic'`
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
