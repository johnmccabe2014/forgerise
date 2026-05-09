import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { SelfCheckInForm } from "@/components/SelfCheckInForm";

const refreshMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: refreshMock }),
}));

describe("SelfCheckInForm", () => {
  beforeEach(() => {
    refreshMock.mockReset();
    vi.stubGlobal("fetch", vi.fn());
  });
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("submits raw scores to the /me proxy and shows the readiness label", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(
      new Response(
        JSON.stringify({ id: "c1", category: 0, submittedBySelf: true }),
        { status: 201, headers: { "content-type": "application/json" } },
      ),
    );

    render(<SelfCheckInForm playerId="player-1" />);
    fireEvent.change(screen.getByLabelText(/sleep last night/i), {
      target: { value: "8" },
    });
    // soreness=2, mood=4, stress=2, fatigue=2 — labels are 1..5 buttons.
    fireEvent.click(screen.getAllByLabelText("2")[0]); // soreness
    fireEvent.click(screen.getAllByLabelText("4")[1]); // mood
    fireEvent.click(screen.getAllByLabelText("2")[2]); // stress
    fireEvent.click(screen.getAllByLabelText("2")[3]); // fatigue
    fireEvent.click(screen.getByRole("button", { name: /submit check-in/i }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("/api/proxy/me/players/player-1/checkins");
    expect((init as RequestInit).method).toBe("POST");
    expect(JSON.parse(String((init as RequestInit).body))).toEqual({
      sleepHours: 8,
      sorenessScore: 2,
      moodScore: 4,
      stressScore: 2,
      fatigueScore: 2,
    });
    await waitFor(() =>
      expect(screen.getByRole("status")).toHaveTextContent(/ready/i),
    );
    expect(refreshMock).toHaveBeenCalled();
  });

  it("shows a friendly error on 403", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(new Response("{}", { status: 403 }));

    render(<SelfCheckInForm playerId="player-2" />);
    fireEvent.click(screen.getByRole("button", { name: /submit check-in/i }));

    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent(/access/i),
    );
  });
});
