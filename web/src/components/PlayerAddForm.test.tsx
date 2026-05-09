import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { PlayerAddForm } from "@/components/PlayerAddForm";

const refreshMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: refreshMock, replace: vi.fn() }),
}));

describe("PlayerAddForm", () => {
  beforeEach(() => {
    refreshMock.mockReset();
    vi.stubGlobal("fetch", vi.fn());
  });
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("posts the chosen jersey + position and clears the form on success", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(
      new Response("{}", {
        status: 201,
        headers: { "content-type": "application/json" },
      }),
    );

    render(<PlayerAddForm teamId="team-1" />);
    fireEvent.change(screen.getByLabelText(/^name$/i), {
      target: { value: "Sam Player" },
    });
    fireEvent.change(screen.getByLabelText(/#/i), { target: { value: "12" } });
    fireEvent.change(screen.getByLabelText(/position/i), {
      target: { value: "Inside Centre" },
    });
    fireEvent.submit(screen.getByRole("form", { name: /add player/i }));

    await waitFor(() => expect(refreshMock).toHaveBeenCalled());
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("/api/proxy/teams/team-1/players");
    expect(JSON.parse((init as RequestInit).body as string)).toEqual({
      displayName: "Sam Player",
      jerseyNumber: 12,
      position: "Inside Centre",
    });
    // Inputs cleared.
    expect(
      (screen.getByLabelText(/^name$/i) as HTMLInputElement).value,
    ).toBe("");
    expect(
      (screen.getByLabelText(/#/i) as HTMLSelectElement).value,
    ).toBe("");
    expect(
      (screen.getByLabelText(/position/i) as HTMLSelectElement).value,
    ).toBe("");
  });

  it("omits jersey and position when the coach leaves them blank", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(
      new Response("{}", {
        status: 201,
        headers: { "content-type": "application/json" },
      }),
    );

    render(<PlayerAddForm teamId="team-1" />);
    fireEvent.change(screen.getByLabelText(/^name$/i), {
      target: { value: "Unassigned Player" },
    });
    fireEvent.submit(screen.getByRole("form", { name: /add player/i }));

    await waitFor(() => expect(refreshMock).toHaveBeenCalled());
    const [, init] = fetchMock.mock.calls[0];
    expect(JSON.parse((init as RequestInit).body as string)).toEqual({
      displayName: "Unassigned Player",
    });
  });

  it("offers Forwards and Backs option groups for position", () => {
    render(<PlayerAddForm teamId="team-1" />);
    const select = screen.getByLabelText(/position/i) as HTMLSelectElement;
    const groupLabels = Array.from(
      select.querySelectorAll("optgroup"),
    ).map((g) => g.getAttribute("label"));
    expect(groupLabels).toEqual(["Forwards", "Backs"]);

    const optionTexts = Array.from(select.options).map((o) => o.textContent);
    expect(optionTexts).toContain("Hooker");
    expect(optionTexts).toContain("Fly-half");
  });

  it("offers jersey numbers 1 through 23 plus a blank default", () => {
    render(<PlayerAddForm teamId="team-1" />);
    const select = screen.getByLabelText(/#/i) as HTMLSelectElement;
    const values = Array.from(select.options).map((o) => o.value);
    expect(values[0]).toBe("");
    expect(values.slice(1)).toEqual(
      Array.from({ length: 23 }, (_, i) => String(i + 1)),
    );
  });

  it("surfaces server validation problem title on 400", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ title: "Validation failed" }), {
        status: 400,
        headers: { "content-type": "application/json" },
      }),
    );

    render(<PlayerAddForm teamId="team-1" />);
    fireEvent.change(screen.getByLabelText(/^name$/i), {
      target: { value: "Sam" },
    });
    fireEvent.submit(screen.getByRole("form", { name: /add player/i }));

    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent(
        /validation failed/i,
      ),
    );
    expect(refreshMock).not.toHaveBeenCalled();
  });
});
