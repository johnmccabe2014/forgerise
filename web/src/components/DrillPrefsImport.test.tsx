import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { DrillPrefsImport } from "@/components/DrillPrefsImport";

const refreshMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: refreshMock }),
}));

describe("DrillPrefsImport", () => {
  beforeEach(() => {
    refreshMock.mockReset();
    vi.stubGlobal("fetch", vi.fn());
  });
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("posts CSV body and shows summary", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(
      new Response(
        JSON.stringify({ applied: 2, cleared: 1, skipped: 0, errors: [] }),
        { status: 200, headers: { "content-type": "application/json" } },
      ),
    );
    render(<DrillPrefsImport teamId="t1" />);

    const ta = screen.getByTestId("drill-prefs-import-textarea");
    fireEvent.change(ta, {
      target: { value: "mobility-flow,favourite\nconditioned-game,clear" },
    });
    fireEvent.click(screen.getByRole("button", { name: /^import$/i }));

    await waitFor(() =>
      expect(screen.getByTestId("drill-prefs-import-result")).toBeTruthy(),
    );
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("/api/proxy/teams/t1/drill-preferences/import");
    expect((init as RequestInit).method).toBe("POST");
    expect(refreshMock).toHaveBeenCalled();
    expect(
      screen.getByTestId("drill-prefs-import-result").textContent,
    ).toContain("Applied 2");
  });

  it("surfaces errors and lists row failures", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          applied: 1,
          cleared: 0,
          skipped: 0,
          errors: [{ line: 3, reason: "unknown drill 'foo'" }],
        }),
        { status: 200, headers: { "content-type": "application/json" } },
      ),
    );
    render(<DrillPrefsImport teamId="t1" />);
    fireEvent.change(screen.getByTestId("drill-prefs-import-textarea"), {
      target: { value: "mobility-flow,favourite\nfoo,favourite" },
    });
    fireEvent.click(screen.getByRole("button", { name: /^import$/i }));

    await waitFor(() =>
      expect(
        screen.getByTestId("drill-prefs-import-result").textContent,
      ).toContain("unknown drill"),
    );
  });
});
