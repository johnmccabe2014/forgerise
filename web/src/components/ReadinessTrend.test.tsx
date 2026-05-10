import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { ReadinessTrend } from "./ReadinessTrend";

describe("ReadinessTrend", () => {
  it("shows an empty hint when there are no check-ins", () => {
    render(<ReadinessTrend checkins={[]} />);
    expect(screen.getByTestId("readiness-trend-empty")).toBeInTheDocument();
  });

  it("renders tiles oldest → newest from a newest-first list", () => {
    render(
      <ReadinessTrend
        checkins={[
          { id: "c", asOf: "2026-05-09T10:00:00Z", category: 2 }, // newest
          { id: "b", asOf: "2026-05-07T10:00:00Z", category: 1 },
          { id: "a", asOf: "2026-05-05T10:00:00Z", category: 0 }, // oldest
        ]}
      />,
    );
    const tiles = screen.getAllByTestId("readiness-trend-tile");
    expect(tiles).toHaveLength(3);
    // Left-to-right should be oldest → newest, so categories: ready, monitor, modify.
    expect(tiles[0]).toHaveAttribute("data-category", "ready");
    expect(tiles[1]).toHaveAttribute("data-category", "monitor");
    expect(tiles[2]).toHaveAttribute("data-category", "modify");
  });

  it("caps the window size", () => {
    const checkins = Array.from({ length: 20 }, (_, i) => ({
      id: `c${i}`,
      asOf: `2026-05-${String(i + 1).padStart(2, "0")}T10:00:00Z`,
      category: i % 4,
    }));
    render(<ReadinessTrend checkins={checkins} windowSize={5} />);
    expect(screen.getAllByTestId("readiness-trend-tile")).toHaveLength(5);
  });
});
