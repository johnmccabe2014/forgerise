import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ReadinessBadge } from "@/components/ReadinessBadge";

describe("<ReadinessBadge />", () => {
  it("renders the human label for each safe category", () => {
    const cases = [
      ["ready", "Ready"],
      ["monitor", "Monitor"],
      ["modify", "Modify Load"],
      ["recovery", "Recovery Focus"],
    ] as const;

    for (const [category, label] of cases) {
      const { unmount } = render(<ReadinessBadge category={category} />);
      const el = screen.getByRole("status");
      expect(el).toHaveTextContent(label);
      expect(el).toHaveAttribute("aria-label", `Readiness: ${label}`);
      unmount();
    }
  });
});
