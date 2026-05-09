import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { PlayerAddForm } from "@/components/PlayerAddForm";

const refreshMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: refreshMock, replace: vi.fn() }),
}));

describe("PlayerAddForm", () => {
  beforeEach(() => {
    refreshMock.mockReset();
    vi.stubGlobal("fetch", vi.fn());
  });
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("posts only the filled fields and clears the inputs on success", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(
      new Response("{}", { status: 201, headers: { "content-type": "application/json" } }),
    );

    render(<PlayerAddForm teamId="team-1" />);
    fireEvent.change(screen.getByLabelText(/^name$/i), {
      target: { value: "Sam Player" },
    });
    fireEvent.change(screen.getByLabelText(/#/i), { target: { value: "12" } });
    fireEvent.submit(screen.getByRole("form", { name: /add player/i }));

    await waitFor(() => expect(refreshMock).toHaveBeenCalled());
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("/api/proxy/teams/team-1/players");
    expect(JSON.parse((init as RequestInit).body as string)).toEqual({
      displayName: "Sam Player",
      jerseyNumber: 12,
    });
    // Inputs cleared.
    expect((screen.getByLabelText(/^name$/i) as HTMLInputElement).value).toBe("");
    expect((screen.getByLabelText(/#/i) as HTMLInputElement).value).toBe("");
  });

  it("rejects out-of-range jersey numbers without calling the API", async () => {
    const fetchMock = vi.mocked(global.fetch);

    render(<PlayerAddForm teamId="team-1" />);
    fireEvent.change(screen.getByLabelText(/^name$/i), {
      target: { value: "Sam" },
    });
    fireEvent.change(screen.getByLabelText(/#/i), { target: { value: "9999" } });
    fireEvent.submit(screen.getByRole("form", { name: /add player/i }));

    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent(/0–999/),
    );
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("surfaces server validation problem title on 400", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ title: "Validation failed" }), {
        status: 400,
        headers: { "content-type": "application/json" },
      }),
    );

    render(<PlayerAddForm teamId="team-1" />);
    fireEvent.change(screen.getByLabelText(/^name$/i), {
      target: { value: "Sam" },
    });
    fireEvent.submit(screen.getByRole("form", { name: /add player/i }));

    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent(/validation failed/i),
    );
    expect(refreshMock).not.toHaveBeenCalled();
  });
});
