import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { ActivityUnreadBadge } from "@/components/ActivityUnreadBadge";

describe("ActivityUnreadBadge", () => {
  const originalFetch = globalThis.fetch;

  beforeEach(() => {
    globalThis.fetch = vi.fn(() =>
      Promise.resolve(new Response(null, { status: 200 })),
    ) as typeof fetch;
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it("renders nothing when unread is zero and does not POST", () => {
    render(<ActivityUnreadBadge teamId="t1" initialUnread={0} />);
    expect(screen.queryByTestId("activity-unread-badge")).toBeNull();
    expect(globalThis.fetch).not.toHaveBeenCalled();
  });

  it("shows the count and POSTs to mark seen, then clears the badge", async () => {
    render(<ActivityUnreadBadge teamId="team-7" initialUnread={3} />);
    expect(screen.getByTestId("activity-unread-badge")).toHaveTextContent(
      "3 new",
    );
    expect(globalThis.fetch).toHaveBeenCalledWith(
      "/api/proxy/teams/team-7/activity/seen",
      expect.objectContaining({ method: "POST" }),
    );
    await waitFor(() =>
      expect(screen.queryByTestId("activity-unread-badge")).toBeNull(),
    );
  });

  it("caps display at 99+", () => {
    render(<ActivityUnreadBadge teamId="t1" initialUnread={99} />);
    expect(screen.getByTestId("activity-unread-badge")).toHaveTextContent(
      "99+ new",
    );
  });
});
