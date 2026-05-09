import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { SelfIncidentForm } from "@/components/SelfIncidentForm";

const refreshMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: refreshMock }),
}));

function severityRadio(value: string): HTMLInputElement {
  const radio = screen
    .getAllByRole("radio")
    .find((r) => (r as HTMLInputElement).value === value);
  if (!radio) throw new Error(`severity radio ${value} not found`);
  return radio as HTMLInputElement;
}

describe("SelfIncidentForm", () => {
  beforeEach(() => {
    refreshMock.mockReset();
    vi.stubGlobal("fetch", vi.fn());
  });
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("offers only Low and Medium severities", () => {
    render(<SelfIncidentForm playerId="p1" />);
    expect(screen.getByText("Low")).toBeInTheDocument();
    expect(screen.getByText("Medium")).toBeInTheDocument();
    expect(screen.queryByText("High")).not.toBeInTheDocument();
  });

  it("posts to /api/proxy/me/players/{id}/incidents and shows confirmation", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ id: "i1", submittedBySelf: true }), {
        status: 201,
        headers: { "content-type": "application/json" },
      }),
    );

    render(<SelfIncidentForm playerId="player-7" />);
    fireEvent.click(severityRadio("0"));
    fireEvent.change(screen.getByPlaceholderText(/tight hamstring/i), {
      target: { value: "Sore knee after match" },
    });
    fireEvent.click(screen.getByRole("button", { name: /log incident/i }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("/api/proxy/me/players/player-7/incidents");
    expect(init?.method).toBe("POST");
    const body = JSON.parse(init!.body as string);
    expect(body).toEqual({
      severity: 0,
      summary: "Sore knee after match",
      notes: null,
    });
    expect(await screen.findByRole("status")).toHaveTextContent(/logged/i);
    expect(refreshMock).toHaveBeenCalled();
  });

  it("surfaces server errors without crashing", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(
      new Response("{}", { status: 400 }),
    );
    render(<SelfIncidentForm playerId="player-7" />);
    fireEvent.click(severityRadio("1"));
    fireEvent.change(screen.getByPlaceholderText(/tight hamstring/i), {
      target: { value: "Twisted ankle" },
    });
    fireEvent.click(screen.getByRole("button", { name: /log incident/i }));

    expect(await screen.findByRole("alert")).toHaveTextContent(/could not log/i);
    expect(refreshMock).not.toHaveBeenCalled();
  });
});
