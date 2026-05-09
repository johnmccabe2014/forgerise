import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { PlayerRow } from "@/components/PlayerRow";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: vi.fn(), replace: vi.fn() }),
}));

describe("PlayerRow", () => {
  const player = {
    id: "p1",
    displayName: "Sam Player",
    jerseyNumber: 12,
    position: "Centre",
  };

  it("links to the player profile page for this team", () => {
    render(<PlayerRow teamId="team-1" player={player} />);
    const link = screen.getByRole("link", { name: /view profile/i });
    expect(link).toHaveAttribute("href", "/teams/team-1/players/p1");
  });
});
