import { render, screen, act } from "@testing-library/react";
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import Splash from "./Splash";

describe("Splash", () => {
  beforeEach(() => {
    window.sessionStorage.clear();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("appears after mount and disappears after hold + fade", () => {
    render(<Splash />);
    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(screen.getByTestId("splash")).toBeInTheDocument();
    act(() => {
      vi.advanceTimersByTime(1200 + 400 + 10);
    });
    expect(screen.queryByTestId("splash")).not.toBeInTheDocument();
  });

  it("does not render when the session flag is already set", () => {
    window.sessionStorage.setItem("forgerise.splashShown", "1");
    render(<Splash />);
    act(() => {
      vi.advanceTimersByTime(10);
    });
    expect(screen.queryByTestId("splash")).not.toBeInTheDocument();
  });

  it("dismisses immediately on click", () => {
    render(<Splash />);
    act(() => {
      vi.advanceTimersByTime(1);
    });
    const overlay = screen.getByTestId("splash");
    act(() => {
      overlay.click();
    });
    expect(screen.queryByTestId("splash")).not.toBeInTheDocument();
  });
});
