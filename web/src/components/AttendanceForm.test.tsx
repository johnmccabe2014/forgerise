import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import {
  AttendanceForm,
  type AttendanceRow,
} from "@/components/AttendanceForm";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: vi.fn(), refresh: vi.fn() }),
}));

const sampleRows: AttendanceRow[] = [
  {
    playerId: "p-1",
    playerDisplayName: "Sam Player",
    status: 0, // Absent
    note: null,
    recordedAt: null,
  },
  {
    playerId: "p-2",
    playerDisplayName: "Riley Player",
    status: 1, // Present
    note: "Knock from last week",
    recordedAt: "2026-05-09T10:00:00Z",
  },
];

describe("AttendanceForm", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
  });
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("renders one row per player with status preselected", () => {
    render(
      <AttendanceForm
        teamId="team-1"
        sessionId="sess-1"
        initialRows={sampleRows}
      />,
    );
    const samStatus = screen.getByLabelText(
      /status for sam player/i,
    ) as HTMLSelectElement;
    const rileyStatus = screen.getByLabelText(
      /status for riley player/i,
    ) as HTMLSelectElement;
    expect(samStatus.value).toBe("0");
    expect(rileyStatus.value).toBe("1");
  });

  it("PUTs the bulk payload and shows a saved confirmation", async () => {
    const fetchMock = vi.mocked(global.fetch);
    const updated: AttendanceRow[] = [
      { ...sampleRows[0], status: 1, recordedAt: "2026-05-09T11:00:00Z" },
      { ...sampleRows[1] },
    ];
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify(updated), {
        status: 200,
        headers: { "content-type": "application/json" },
      }),
    );

    render(
      <AttendanceForm
        teamId="team-1"
        sessionId="sess-1"
        initialRows={sampleRows}
      />,
    );

    fireEvent.change(screen.getByLabelText(/status for sam player/i), {
      target: { value: "1" }, // Present
    });
    fireEvent.change(screen.getByLabelText(/note for sam player/i), {
      target: { value: "  On time  " },
    });

    fireEvent.submit(screen.getByRole("form", { name: /record attendance/i }));

    await waitFor(() =>
      expect(screen.getByRole("status")).toHaveTextContent(/saved/i),
    );

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("/api/proxy/teams/team-1/sessions/sess-1/attendance");
    expect((init as RequestInit).method).toBe("PUT");
    const body = JSON.parse((init as RequestInit).body as string);
    expect(body.items).toEqual([
      { playerId: "p-1", status: 1, note: "On time" },
      { playerId: "p-2", status: 1, note: "Knock from last week" },
    ]);
  });

  it("surfaces an error alert and skips status update when API fails", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ title: "Validation failed" }), {
        status: 400,
        headers: { "content-type": "application/json" },
      }),
    );

    render(
      <AttendanceForm
        teamId="team-1"
        sessionId="sess-1"
        initialRows={sampleRows}
      />,
    );

    fireEvent.submit(screen.getByRole("form", { name: /record attendance/i }));

    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent(/validation failed/i),
    );
    expect(screen.queryByRole("status")).toBeNull();
  });

  it("shows a friendly empty state when the team has no players yet", () => {
    render(
      <AttendanceForm teamId="team-1" sessionId="sess-1" initialRows={[]} />,
    );
    expect(screen.getByText(/add players to the team/i)).toBeInTheDocument();
  });
});
