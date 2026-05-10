import { render, screen, act } from "@testing-library/react";
import { describe, it, expect, beforeEach, vi } from "vitest";
import Splash from "./Splash";

describe("Splash", () => {
  beforeEach(() => {
    window.sessionStorage.clear();
    vi.useFakeTimers();
  });

  it("shows on first load and disappears after the hold + fade window", () => {
    render(<Splash />);
    expect(screen.getByTestId("splash")).toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(1200 + 400 + 10);
    });
    expect(screen.queryByTestId("splash")).not.toBeInTheDocument();
    vi.useRealTimers();
  });

  it("does not render again within the same session", () => {
    window.sessionStorage.setItem("forgerise.splashShown", "1");
    render(<Splash />);
    expect(screen.queryByTestId("splash")).not.toBeInTheDocument();
    vi.useRealTimers();
  });
});
