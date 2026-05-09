import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ActivityFeed, type ActivityEvent } from "@/components/ActivityFeed";

const SAMPLE: ActivityEvent[] = [
  {
    kind: "checkin_self_submitted",
    at: new Date(Date.now() - 30 * 60 * 1000).toISOString(),
    playerId: "p1",
    playerDisplayName: "Sam Self",
    subjectId: "c1",
    category: 1,
    categoryLabel: "Monitor",
    severity: null,
    summary: null,
    acknowledged: null,
  },
  {
    kind: "incident_self_reported",
    at: new Date(Date.now() - 60 * 60 * 1000).toISOString(),
    playerId: "p2",
    playerDisplayName: "Mini Mouse",
    subjectId: "i1",
    category: null,
    categoryLabel: null,
    severity: 0,
    summary: "Sore knee",
    acknowledged: false,
  },
  {
    kind: "invite_redeemed",
    at: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
    playerId: "p3",
    playerDisplayName: "New Joiner",
    subjectId: null,
    category: null,
    categoryLabel: null,
    severity: null,
    summary: null,
    acknowledged: null,
  },
];

describe("ActivityFeed", () => {
  it("renders the empty state when there is no activity", () => {
    render(<ActivityFeed teamId="t1" events={[]} />);
    expect(screen.getByText(/no recent player activity/i)).toBeInTheDocument();
  });

  it("renders each event kind with player name and a sensible label", () => {
    render(<ActivityFeed teamId="t1" events={SAMPLE} />);
    expect(screen.getByText("Sam Self")).toBeInTheDocument();
    expect(screen.getByText(/submitted a check-in — Monitor/i)).toBeInTheDocument();

    expect(screen.getByText("Mini Mouse")).toBeInTheDocument();
    expect(screen.getByText(/reported a low incident — needs review/i)).toBeInTheDocument();
    expect(screen.getByText("Sore knee")).toBeInTheDocument();

    expect(screen.getByText("New Joiner")).toBeInTheDocument();
    expect(screen.getByText(/claimed their roster spot/i)).toBeInTheDocument();
  });

  it("links each row to the player profile", () => {
    render(<ActivityFeed teamId="team-9" events={SAMPLE} />);
    const link = screen.getByRole("link", { name: "Sam Self" }) as HTMLAnchorElement;
    expect(link.getAttribute("href")).toBe("/teams/team-9/players/p1");
  });
});
