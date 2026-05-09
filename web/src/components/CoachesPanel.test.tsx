import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { CoachesPanel, type CoachRow } from "@/components/CoachesPanel";

const refreshMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: refreshMock }),
}));

const sample: CoachRow[] = [
  {
    userId: "u-owner",
    displayName: "Owner Person",
    email: "owner@example.com",
    role: "owner",
    joinedAt: "2025-01-01T00:00:00Z",
  },
  {
    userId: "u-coach",
    displayName: "Other Coach",
    email: "coach@example.com",
    role: "coach",
    joinedAt: "2025-02-01T00:00:00Z",
  },
];

describe("CoachesPanel", () => {
  beforeEach(() => {
    refreshMock.mockReset();
    vi.stubGlobal("fetch", vi.fn());
    vi.stubGlobal("confirm", vi.fn(() => true));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("hides the remove button for non-owners", () => {
    render(
      <CoachesPanel
        teamId="t1"
        myRole="coach"
        myUserId="u-coach"
        coaches={sample}
      />,
    );
    expect(screen.queryByRole("button", { name: /remove/i })).toBeNull();
  });

  it("owner can remove another coach", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));
    render(
      <CoachesPanel
        teamId="t1"
        myRole="owner"
        myUserId="u-owner"
        coaches={sample}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: /remove other coach/i }));
    await waitFor(() => expect(refreshMock).toHaveBeenCalled());
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("/api/proxy/teams/t1/coaches/u-coach");
    expect((init as RequestInit).method).toBe("DELETE");
  });

  it("shows last-owner error on 409", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(new Response("{}", { status: 409 }));
    // Two owners so the remove button shows up for the other owner.
    const data: CoachRow[] = [
      sample[0],
      { ...sample[1], role: "owner" },
    ];
    render(
      <CoachesPanel
        teamId="t1"
        myRole="owner"
        myUserId="u-owner"
        coaches={data}
      />,
    );
    fireEvent.click(screen.getByRole("button", { name: /remove other coach/i }));
    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent(/last owner/i),
    );
  });
});
