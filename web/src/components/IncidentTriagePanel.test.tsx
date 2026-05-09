import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import {
  IncidentTriagePanel,
  type UnacknowledgedIncident,
} from "@/components/IncidentTriagePanel";

const refreshMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: refreshMock }),
}));

const SAMPLE: UnacknowledgedIncident[] = [
  {
    id: "inc-1",
    playerId: "player-9",
    playerDisplayName: "Sam Filer",
    occurredAt: "2026-05-09T10:00:00Z",
    severity: 1,
    summary: "Sore knee after sprints",
  },
];

describe("IncidentTriagePanel", () => {
  beforeEach(() => {
    refreshMock.mockReset();
    vi.stubGlobal("fetch", vi.fn());
  });
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("renders the empty state when there are no unread reports", () => {
    render(<IncidentTriagePanel teamId="team-1" incidents={[]} />);
    expect(screen.getByText(/no unread player reports/i)).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: /acknowledge/i }),
    ).not.toBeInTheDocument();
  });

  it("acknowledges via the API and refreshes the page", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ id: "inc-1", acknowledgedAt: "now" }), {
        status: 200,
        headers: { "content-type": "application/json" },
      }),
    );

    render(<IncidentTriagePanel teamId="team-1" incidents={SAMPLE} />);
    expect(screen.getByText("Sam Filer")).toBeInTheDocument();
    expect(screen.getByText("Medium")).toBeInTheDocument();
    expect(screen.getByText("Sore knee after sprints")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /acknowledge/i }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe(
      "/api/proxy/teams/team-1/players/player-9/incidents/inc-1/acknowledge",
    );
    expect(init?.method).toBe("POST");
    await waitFor(() => expect(refreshMock).toHaveBeenCalled());
  });

  it("surfaces server errors without crashing", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(new Response("{}", { status: 500 }));

    render(<IncidentTriagePanel teamId="team-1" incidents={SAMPLE} />);
    fireEvent.click(screen.getByRole("button", { name: /acknowledge/i }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      /could not acknowledge/i,
    );
    expect(refreshMock).not.toHaveBeenCalled();
  });
});
