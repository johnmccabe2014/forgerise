import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { BrandMark } from "./BrandMark";

describe("BrandMark", () => {
  it("renders the wordmark and a link to dashboard by default", () => {
    render(<BrandMark />);
    expect(screen.getByTestId("brand-mark")).toBeInTheDocument();
    expect(screen.getByText("ForgeRise")).toBeInTheDocument();
    const link = screen.getByRole("link", { name: /forgerise home/i });
    expect(link).toHaveAttribute("href", "/dashboard");
  });

  it("can hide the wordmark for icon-only contexts", () => {
    render(<BrandMark showWordmark={false} />);
    expect(screen.queryByText("ForgeRise")).toBeNull();
  });

  it("renders without a link when href is null", () => {
    render(<BrandMark href={null} />);
    expect(screen.queryByRole("link")).toBeNull();
    expect(screen.getByTestId("brand-mark")).toBeInTheDocument();
  });
});
