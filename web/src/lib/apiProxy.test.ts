import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { rewriteSetCookie, proxyToApi } from "@/lib/apiProxy";

describe("rewriteSetCookie", () => {
  it("strips Domain attribute", () => {
    const out = rewriteSetCookie(
      "fr_csrf=abc; Path=/; Domain=api.local; SameSite=Lax; Secure",
    );
    expect(out).not.toMatch(/Domain=/i);
    expect(out).toContain("Path=/");
    expect(out).toContain("SameSite=Lax");
  });

  it("remaps Path=/auth to /api/proxy/auth", () => {
    const out = rewriteSetCookie(
      "fr_rt=xyz; Path=/auth; HttpOnly; SameSite=Lax",
    );
    expect(out).toContain("Path=/api/proxy/auth");
    expect(out).not.toMatch(/Path=\/auth\b/);
  });

  it("leaves other Path values untouched", () => {
    const out = rewriteSetCookie("fr_at=abc; Path=/; HttpOnly");
    expect(out).toContain("Path=/");
  });
});

describe("proxyToApi", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  function makeReq(opts: {
    method: string;
    cookie?: string;
    body?: string;
    correlation?: string;
  }) {
    const headers = new Headers();
    headers.set("content-type", "application/json");
    if (opts.cookie) headers.set("cookie", opts.cookie);
    if (opts.correlation) headers.set("x-correlation-id", opts.correlation);
    headers.set("authorization", "Bearer LEAKY-TOKEN");
    return {
      method: opts.method,
      headers,
      nextUrl: new URL("http://localhost:3000/api/proxy/auth/login"),
      arrayBuffer: async () =>
        opts.body ? new TextEncoder().encode(opts.body).buffer : new ArrayBuffer(0),
    } as unknown as import("next/server").NextRequest;
  }

  it("injects X-CSRF-Token from fr_csrf cookie on POST and forwards correlation id", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(
      new Response("{}", { status: 200, headers: { "content-type": "application/json" } }),
    );

    const req = makeReq({
      method: "POST",
      cookie: "fr_csrf=token-123; fr_at=opaque",
      body: JSON.stringify({ email: "a@b" }),
      correlation: "corr-abc",
    });

    await proxyToApi(req, ["auth", "login"]);

    expect(fetchMock).toHaveBeenCalledOnce();
    const callArgs = fetchMock.mock.calls[0];
    const url = callArgs[0];
    const init = callArgs[1] as RequestInit;
    expect(String(url)).toMatch(/\/auth\/login$/);
    const sent = init.headers as Headers;
    expect(sent.get("X-CSRF-Token")).toBe("token-123");
    expect(sent.get("cookie")).toContain("fr_csrf=token-123");
    expect(sent.get("x-correlation-id")).toBe("corr-abc");
    // Authorization from browser MUST be stripped — only cookie auth allowed.
    expect(sent.get("authorization")).toBeNull();
  });

  it("does not inject CSRF for GET", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(new Response("[]", { status: 200 }));

    const req = makeReq({ method: "GET", cookie: "fr_csrf=xyz" });
    await proxyToApi(req, ["teams"]);

    const init = fetchMock.mock.calls[0][1] as RequestInit;
    const sent = init.headers as Headers;
    expect(sent.get("X-CSRF-Token")).toBeNull();
  });

  it("returns a null body for 204 No Content (fetch spec forbids body on null-body status)", async () => {
    const fetchMock = vi.mocked(global.fetch);
    // Upstream NoContent — Kestrel sends 204 with no body, but Node fetch
    // would still let us read an empty ArrayBuffer; our proxy must not
    // forward that buffer or `new Response(buf, {status: 204})` throws.
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));

    const req = makeReq({
      method: "POST",
      cookie: "fr_csrf=t; fr_at=a",
      body: "",
    });
    const res = await proxyToApi(req, ["auth", "logout"]);

    expect(res.status).toBe(204);
    expect(res.body).toBeNull();
  });
});
