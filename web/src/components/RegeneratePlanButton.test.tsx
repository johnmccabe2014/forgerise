import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { RegeneratePlanButton } from "@/components/RegeneratePlanButton";

const pushMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: pushMock }),
}));

describe("RegeneratePlanButton", () => {
  const originalFetch = globalThis.fetch;

  beforeEach(() => {
    pushMock.mockReset();
    globalThis.fetch = vi.fn(() =>
      Promise.resolve(
        new Response(JSON.stringify({ id: "new-plan-id" }), {
          status: 201,
          headers: { "Content-Type": "application/json" },
        }),
      ),
    ) as typeof fetch;
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it("POSTs the same focus and routes to the new plan", async () => {
    render(<RegeneratePlanButton teamId="t1" focus="Lineout" />);
    fireEvent.click(screen.getByRole("button", { name: /regenerate/i }));
    await waitFor(() =>
      expect(pushMock).toHaveBeenCalledWith("/teams/t1/session-plans/new-plan-id"),
    );
    expect(globalThis.fetch).toHaveBeenCalledWith(
      "/api/proxy/teams/t1/session-plans/generate",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({ focus: "Lineout" }),
      }),
    );
  });

  it("surfaces an error on failure", async () => {
    globalThis.fetch = vi.fn(() =>
      Promise.resolve(new Response(null, { status: 500 })),
    ) as typeof fetch;
    render(<RegeneratePlanButton teamId="t1" focus="Lineout" />);
    fireEvent.click(screen.getByRole("button", { name: /regenerate/i }));
    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent(/could not regenerate/i),
    );
    expect(pushMock).not.toHaveBeenCalled();
  });
});
