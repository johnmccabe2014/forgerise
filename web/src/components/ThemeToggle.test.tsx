import { render, screen, act } from "@testing-library/react";
import { describe, it, expect, beforeEach, vi } from "vitest";
import ThemeToggle from "./ThemeToggle";

describe("ThemeToggle", () => {
  beforeEach(() => {
    window.localStorage.clear();
    document.documentElement.classList.remove("dark");
  });

  it("toggles the dark class on the root and persists the choice", () => {
    // jsdom: matchMedia is missing by default
    Object.defineProperty(window, "matchMedia", {
      writable: true,
      value: vi.fn().mockReturnValue({
        matches: false,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
      }),
    });

    render(<ThemeToggle />);
    const btn = screen.getByTestId("theme-toggle");
    expect(document.documentElement.classList.contains("dark")).toBe(false);

    act(() => {
      btn.click();
    });
    expect(document.documentElement.classList.contains("dark")).toBe(true);
    expect(window.localStorage.getItem("forgerise.theme")).toBe("dark");

    act(() => {
      btn.click();
    });
    expect(document.documentElement.classList.contains("dark")).toBe(false);
    expect(window.localStorage.getItem("forgerise.theme")).toBe("light");
  });
});
