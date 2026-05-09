import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { IncidentAckButton } from "@/components/IncidentAckButton";

const refreshMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: refreshMock }),
}));

describe("IncidentAckButton", () => {
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

  it("POSTs to the acknowledge endpoint and refreshes", async () => {
    render(
      <IncidentAckButton
        teamId="t1"
        playerId="p2"
        incidentId="i3"
      />,
    );
    fireEvent.click(screen.getByRole("button", { name: /acknowledge/i }));
    await waitFor(() => expect(refreshMock).toHaveBeenCalled());
    expect(globalThis.fetch).toHaveBeenCalledWith(
      "/api/proxy/teams/t1/players/p2/incidents/i3/acknowledge",
      expect.objectContaining({ method: "POST" }),
    );
  });

  it("shows an error message when the API returns non-OK", async () => {
    globalThis.fetch = vi.fn(() =>
      Promise.resolve(new Response(null, { status: 500 })),
    ) as typeof fetch;
    render(
      <IncidentAckButton teamId="t1" playerId="p2" incidentId="i3" />,
    );
    fireEvent.click(screen.getByRole("button", { name: /acknowledge/i }));
    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent(
        /could not acknowledge/i,
      ),
    );
    expect(refreshMock).not.toHaveBeenCalled();
  });
});
