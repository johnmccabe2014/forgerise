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
  return res;
}

export const config = {
  matcher: "/((?!_next/static|_next/image|favicon.ico).*)",
};
