import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { AdoptPlanForm } from "@/components/AdoptPlanForm";

const refreshMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: refreshMock }),
}));

describe("AdoptPlanForm", () => {
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

  it("opens the form, POSTs schedule, and refreshes", async () => {
    render(<AdoptPlanForm teamId="t1" planId="p1" />);
    fireEvent.click(screen.getByRole("button", { name: /mark as used/i }));
    fireEvent.click(screen.getByRole("button", { name: /adopt as session/i }));
    await waitFor(() => expect(refreshMock).toHaveBeenCalled());
    const call = (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(call[0]).toBe("/api/proxy/teams/t1/session-plans/p1/adopt");
    const body = JSON.parse((call[1] as RequestInit).body as string);
    expect(body.durationMinutes).toBe(75);
    expect(body.type).toBe(0);
    expect(typeof body.scheduledAt).toBe("string");
  });

  it("shows error on failure", async () => {
    globalThis.fetch = vi.fn(() =>
      Promise.resolve(new Response(null, { status: 400 })),
    ) as typeof fetch;
    render(<AdoptPlanForm teamId="t1" planId="p1" />);
    fireEvent.click(screen.getByRole("button", { name: /mark as used/i }));
    fireEvent.click(screen.getByRole("button", { name: /adopt as session/i }));
    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent(/could not adopt/i),
    );
    expect(refreshMock).not.toHaveBeenCalled();
  });
});
