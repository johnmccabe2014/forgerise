import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { SessionCreateForm } from "@/components/SessionCreateForm";

const replaceMock = vi.fn();
const refreshMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: replaceMock, refresh: refreshMock }),
}));

describe("SessionCreateForm", () => {
  beforeEach(() => {
    replaceMock.mockReset();
    refreshMock.mockReset();
    vi.stubGlobal("fetch", vi.fn());
  });
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("posts the chosen schedule and redirects to the attendance page on 201", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ id: "session-9" }), {
        status: 201,
        headers: { "content-type": "application/json" },
      }),
    );

    render(<SessionCreateForm teamId="team-1" />);

    fireEvent.change(screen.getByLabelText(/^date$/i), {
      target: { value: "2026-06-15" },
    });
    fireEvent.change(screen.getByLabelText(/^time$/i), {
      target: { value: "19:30" },
    });
    fireEvent.change(screen.getByLabelText(/duration/i), {
      target: { value: "75" },
    });
    fireEvent.change(screen.getByLabelText(/^type$/i), {
      target: { value: "1" }, // Match
    });
    fireEvent.change(screen.getByLabelText(/location/i), {
      target: { value: "Riverside Pitch 2" },
    });
    fireEvent.change(screen.getByLabelText(/focus/i), {
      target: { value: "Set piece" },
    });

    fireEvent.submit(screen.getByRole("form", { name: /create session/i }));

    await waitFor(() =>
      expect(replaceMock).toHaveBeenCalledWith(
        "/teams/team-1/sessions/session-9/attendance",
      ),
    );
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("/api/proxy/teams/team-1/sessions");
    expect((init as RequestInit).method).toBe("POST");
    const body = JSON.parse((init as RequestInit).body as string);
    expect(body.durationMinutes).toBe(75);
    expect(body.type).toBe(1);
    expect(body.location).toBe("Riverside Pitch 2");
    expect(body.focus).toBe("Set piece");
    // scheduledAt should be a parseable ISO that matches the local 2026-06-15 19:30.
    const parsed = new Date(body.scheduledAt);
    expect(Number.isNaN(parsed.getTime())).toBe(false);
    const expected = new Date("2026-06-15T19:30:00").toISOString();
    expect(body.scheduledAt).toBe(expected);
  });

  it("blocks an out-of-range duration without calling the API", async () => {
    const fetchMock = vi.mocked(global.fetch);
    render(<SessionCreateForm teamId="team-1" />);
    fireEvent.change(screen.getByLabelText(/duration/i), {
      target: { value: "999" },
    });
    fireEvent.submit(screen.getByRole("form", { name: /create session/i }));

    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent(/5 and 480/),
    );
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("surfaces ProblemDetails title on 400", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ title: "Schedule must be in the future" }), {
        status: 400,
        headers: { "content-type": "application/json" },
      }),
    );

    render(<SessionCreateForm teamId="team-1" />);
    fireEvent.submit(screen.getByRole("form", { name: /create session/i }));

    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent(
        /schedule must be in the future/i,
      ),
    );
    expect(replaceMock).not.toHaveBeenCalled();
  });
});
