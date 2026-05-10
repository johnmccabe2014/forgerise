import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { ArchivePlanButton } from "@/components/ArchivePlanButton";

const refreshMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: refreshMock }),
}));

describe("ArchivePlanButton", () => {
  const originalFetch = globalThis.fetch;

  beforeEach(() => {
    refreshMock.mockReset();
    globalThis.fetch = vi.fn(() =>
      Promise.resolve(new Response(null, { status: 200 })),
    ) as typeof fetch;
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it("POSTs to the archive endpoint and refreshes on success", async () => {
    render(
      <ArchivePlanButton teamId="t1" planId="p1" archived={false} adopted={false} />,
    );
    fireEvent.click(screen.getByTestId("plan-archive-button"));
    await waitFor(() => expect(refreshMock).toHaveBeenCalled());
    expect(globalThis.fetch).toHaveBeenCalledWith(
      "/api/proxy/teams/t1/session-plans/p1/archive",
      expect.objectContaining({ method: "POST" }),
    );
  });

  it("shows Restore when already archived", () => {
    render(
      <ArchivePlanButton teamId="t1" planId="p1" archived={true} adopted={false} />,
    );
    expect(screen.getByTestId("plan-archive-button")).toHaveTextContent(/restore/i);
  });

  it("hides itself for adopted (non-archived) plans", () => {
    const { container } = render(
      <ArchivePlanButton teamId="t1" planId="p1" archived={false} adopted={true} />,
    );
    expect(container.firstChild).toBeNull();
  });

  it("surfaces the API’s plan-error message on 400", async () => {
    globalThis.fetch = vi.fn(() =>
      Promise.resolve(
        new Response(
          JSON.stringify({
            errors: { plan: ["adopted plans cannot be archived"] },
          }),
          { status: 400, headers: { "content-type": "application/json" } },
        ),
      ),
    ) as typeof fetch;
    render(
      <ArchivePlanButton teamId="t1" planId="p1" archived={false} adopted={false} />,
    );
    fireEvent.click(screen.getByTestId("plan-archive-button"));
    await waitFor(() =>
      expect(screen.getByTestId("plan-archive-error")).toHaveTextContent(
        /adopted plans cannot be archived/i,
      ),
    );
  });
});
