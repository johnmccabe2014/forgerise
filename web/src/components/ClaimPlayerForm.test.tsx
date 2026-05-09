import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { ClaimPlayerForm } from "@/components/ClaimPlayerForm";

const refreshMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: refreshMock }),
}));

describe("ClaimPlayerForm", () => {
  beforeEach(() => {
    refreshMock.mockReset();
    vi.stubGlobal("fetch", vi.fn());
  });
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("posts the code and refreshes on success", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          playerId: "p1",
          teamId: "t1",
          playerDisplayName: "x",
          teamName: "y",
        }),
        { status: 200, headers: { "content-type": "application/json" } },
      ),
    );

    render(<ClaimPlayerForm />);
    fireEvent.change(screen.getByLabelText(/player invite code/i), {
      target: { value: "AbCdEfGhIjKl" },
    });
    fireEvent.click(screen.getByRole("button", { name: /claim profile/i }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("/api/proxy/player-invites/redeem");
    expect(JSON.parse(String((init as RequestInit).body))).toEqual({
      code: "AbCdEfGhIjKl",
    });
    await waitFor(() => expect(refreshMock).toHaveBeenCalled());
  });

  it("surfaces a friendly message when the code has expired", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ error: "invite_expired" }), {
        status: 409,
        headers: { "content-type": "application/json" },
      }),
    );

    render(<ClaimPlayerForm />);
    fireEvent.change(screen.getByLabelText(/player invite code/i), {
      target: { value: "ExpiredCode01" },
    });
    fireEvent.click(screen.getByRole("button", { name: /claim profile/i }));

    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent(/expired/i),
    );
  });
});
