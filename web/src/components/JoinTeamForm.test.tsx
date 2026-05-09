import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { JoinTeamForm } from "@/components/JoinTeamForm";

const refreshMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: refreshMock }),
}));

describe("JoinTeamForm", () => {
  beforeEach(() => {
    refreshMock.mockReset();
    vi.stubGlobal("fetch", vi.fn());
  });
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("posts the trimmed code to /teams/join and refreshes on success", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ id: "team-1" }), { status: 200 }),
    );
    render(<JoinTeamForm />);

    fireEvent.change(screen.getByLabelText(/^invite code$/i), {
      target: { value: "  abc12345  " },
    });
    fireEvent.submit(screen.getByRole("form", { name: /join team/i }));

    await waitFor(() => expect(refreshMock).toHaveBeenCalled());
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("/api/proxy/teams/join");
    expect((init as RequestInit).method).toBe("POST");
    expect(JSON.parse((init as RequestInit).body as string)).toEqual({
      code: "abc12345",
    });
  });

  it("rejects very short codes without a network call", async () => {
    const fetchMock = vi.mocked(global.fetch);
    render(<JoinTeamForm />);
    fireEvent.change(screen.getByLabelText(/^invite code$/i), {
      target: { value: "abc" },
    });
    fireEvent.submit(screen.getByRole("form", { name: /join team/i }));
    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent(/full invite code/i),
    );
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("surfaces the consumed-code error on 409", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ error: "invite_consumed" }), {
        status: 409,
        headers: { "content-type": "application/json" },
      }),
    );
    render(<JoinTeamForm />);
    fireEvent.change(screen.getByLabelText(/^invite code$/i), {
      target: { value: "abcdef12" },
    });
    fireEvent.submit(screen.getByRole("form", { name: /join team/i }));
    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent(/already been used/i),
    );
  });

  it("shows a friendly message on 404", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(new Response("{}", { status: 404 }));
    render(<JoinTeamForm />);
    fireEvent.change(screen.getByLabelText(/^invite code$/i), {
      target: { value: "no-such-code" },
    });
    fireEvent.submit(screen.getByRole("form", { name: /join team/i }));
    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent(/isn't recognised/i),
    );
  });
});
