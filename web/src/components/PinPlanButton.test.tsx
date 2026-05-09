import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { PinPlanButton } from "@/components/PinPlanButton";

const refreshMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: refreshMock }),
}));

describe("PinPlanButton", () => {
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

  it("POSTs to the pin endpoint and refreshes on success", async () => {
    render(<PinPlanButton teamId="t1" planId="p1" pinned={false} />);
    fireEvent.click(screen.getByTestId("plan-pin-button"));
    await waitFor(() => expect(refreshMock).toHaveBeenCalled());
    expect(globalThis.fetch).toHaveBeenCalledWith(
      "/api/proxy/teams/t1/session-plans/p1/pin",
      expect.objectContaining({ method: "POST" }),
    );
  });

  it("renders pinned state and surfaces errors", async () => {
    globalThis.fetch = vi.fn(() =>
      Promise.resolve(new Response(null, { status: 500 })),
    ) as typeof fetch;
    render(<PinPlanButton teamId="t1" planId="p1" pinned={true} />);
    expect(screen.getByTestId("plan-pin-button")).toHaveTextContent(/pinned/i);
    fireEvent.click(screen.getByTestId("plan-pin-button"));
    await waitFor(() =>
      expect(screen.getByTestId("plan-pin-error")).toBeInTheDocument(),
    );
    expect(refreshMock).not.toHaveBeenCalled();
  });
});
