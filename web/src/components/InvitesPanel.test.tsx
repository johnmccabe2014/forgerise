import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { InvitesPanel, type InviteRow } from "@/components/InvitesPanel";

const refreshMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: refreshMock }),
}));

describe("InvitesPanel", () => {
  beforeEach(() => {
    refreshMock.mockReset();
    vi.stubGlobal("fetch", vi.fn());
    vi.stubGlobal("confirm", vi.fn(() => true));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("posts to create an invite and refreshes", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(new Response("{}", { status: 201 }));
    render(<InvitesPanel teamId="t1" invites={[]} />);

    fireEvent.click(screen.getByRole("button", { name: /generate invite/i }));
    await waitFor(() => expect(refreshMock).toHaveBeenCalled());
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("/api/proxy/teams/t1/invites");
    expect((init as RequestInit).method).toBe("POST");
  });

  it("shows revoke for active invites and revokes via DELETE", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));
    const future = new Date(Date.now() + 86_400_000).toISOString();
    const invites: InviteRow[] = [
      {
        id: "inv-1",
        code: "abc123",
        createdAt: new Date().toISOString(),
        expiresAt: future,
        consumedAt: null,
        revokedAt: null,
      },
    ];
    render(<InvitesPanel teamId="t1" invites={invites} />);

    fireEvent.click(screen.getByRole("button", { name: /^revoke$/i }));
    await waitFor(() => expect(refreshMock).toHaveBeenCalled());
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("/api/proxy/teams/t1/invites/inv-1");
    expect((init as RequestInit).method).toBe("DELETE");
  });

  it("hides revoke for consumed invites", () => {
    const past = new Date(Date.now() - 86_400_000).toISOString();
    const invites: InviteRow[] = [
      {
        id: "inv-2",
        code: "xyz",
        createdAt: past,
        expiresAt: past,
        consumedAt: past,
        revokedAt: null,
      },
    ];
    render(<InvitesPanel teamId="t1" invites={invites} />);
    expect(screen.queryByRole("button", { name: /^revoke$/i })).toBeNull();
  });
});
