/**
 * Server-side helper for React Server Components to call the .NET API
 * with the user's cookies attached.
 *
 * Forwards cookies from `next/headers`. Never used in the browser.
 */
import { cookies, headers } from "next/headers";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080";

export type ApiResult<T> =
  | { ok: true; status: number; data: T }
  | { ok: false; status: number; error?: unknown };

export async function serverFetchApi<T>(
  path: string,
  init: RequestInit = {},
): Promise<ApiResult<T>> {
  const cookieStore = await cookies();
  const cookieHeader = cookieStore
    .getAll()
    .map((c) => `${c.name}=${encodeURIComponent(c.value)}`)
    .join("; ");

  const headerStore = await headers();
  const correlation = headerStore.get("x-correlation-id") ?? undefined;

  const h = new Headers(init.headers);
  h.set("accept", "application/json");
  if (cookieHeader) h.set("cookie", cookieHeader);
  if (correlation) h.set("x-correlation-id", correlation);

  let res: Response;
  try {
    res = await fetch(API_BASE + path, {
      ...init,
      headers: h,
      cache: "no-store",
    });
  } catch {
    return { ok: false, status: 0, error: "upstream-unavailable" };
  }

  if (res.status === 204) return { ok: true, status: 204, data: undefined as T };
  const text = await res.text();
  let body: unknown = undefined;
  if (text.length > 0) {
    try {
      body = JSON.parse(text);
    } catch {
      body = text;
    }
  }
  if (!res.ok) return { ok: false, status: res.status, error: body };
  return { ok: true, status: res.status, data: body as T };
}
