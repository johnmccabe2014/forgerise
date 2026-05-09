import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { DrillPreferenceToggle } from "@/components/DrillPreferenceToggle";

const refreshMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: refreshMock }),
}));

describe("DrillPreferenceToggle", () => {
  const originalFetch = globalThis.fetch;

  beforeEach(() => {
    refreshMock.mockReset();
    globalThis.fetch = vi.fn(() =>
      Promise.resolve(new Response(null, { status: 204 })),
    ) as typeof fetch;
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it("PUTs favourite when the favourite button is clicked from neutral", async () => {
    render(
      <DrillPreferenceToggle teamId="t1" drillId="mobility-flow" current={null} />,
    );
    fireEvent.click(screen.getByRole("button", { name: /mark mobility-flow as favourite/i }));
    await waitFor(() => expect(refreshMock).toHaveBeenCalled());
    expect(globalThis.fetch).toHaveBeenCalledWith(
      "/api/proxy/teams/t1/drill-preferences/mobility-flow",
      expect.objectContaining({
        method: "PUT",
        body: JSON.stringify({ status: "favourite" }),
      }),
    );
  });

  it("DELETEs the row when the active button is tapped again", async () => {
    render(
      <DrillPreferenceToggle teamId="t1" drillId="conditioned-game" current="exclude" />,
    );
    fireEvent.click(
      screen.getByRole("button", { name: /exclude conditioned-game/i }),
    );
    await waitFor(() => expect(refreshMock).toHaveBeenCalled());
    expect(globalThis.fetch).toHaveBeenCalledWith(
      "/api/proxy/teams/t1/drill-preferences/conditioned-game",
      expect.objectContaining({ method: "DELETE" }),
    );
  });

  it("surfaces an error when the API returns non-OK", async () => {
    globalThis.fetch = vi.fn(() =>
      Promise.resolve(new Response(null, { status: 500 })),
    ) as typeof fetch;
    render(
      <DrillPreferenceToggle teamId="t1" drillId="mobility-flow" current={null} />,
    );
    fireEvent.click(
      screen.getByRole("button", { name: /mark mobility-flow as favourite/i }),
    );
    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent(/could not save/i),
    );
    expect(refreshMock).not.toHaveBeenCalled();
  });
});
