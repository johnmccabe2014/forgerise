import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { GeneratePlanForm } from "@/components/GeneratePlanForm";

const replaceMock = vi.fn();
const refreshMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: replaceMock, refresh: refreshMock }),
}));

describe("GeneratePlanForm", () => {
  beforeEach(() => {
    replaceMock.mockReset();
    refreshMock.mockReset();
    vi.stubGlobal("fetch", vi.fn());
  });
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("posts the focus and navigates to the new plan on success", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ id: "plan-9" }), {
        status: 201,
        headers: { "content-type": "application/json" },
      }),
    );

    render(<GeneratePlanForm teamId="team-1" />);
    fireEvent.change(screen.getByLabelText(/focus override/i), {
      target: { value: "Lineout pods" },
    });
    fireEvent.click(screen.getByRole("button", { name: /generate plan/i }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("/api/proxy/teams/team-1/session-plans/generate");
    expect((init as RequestInit).method).toBe("POST");
    expect(JSON.parse(String((init as RequestInit).body))).toEqual({
      focus: "Lineout pods",
    });
    await waitFor(() =>
      expect(replaceMock).toHaveBeenCalledWith(
        "/teams/team-1/session-plans/plan-9",
      ),
    );
  });

  it("sends a null focus when the field is left blank", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ id: "plan-1" }), {
        status: 201,
        headers: { "content-type": "application/json" },
      }),
    );

    render(<GeneratePlanForm teamId="team-2" redirectToDetail={false} />);
    fireEvent.click(screen.getByRole("button", { name: /generate plan/i }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
    const init = fetchMock.mock.calls[0][1] as RequestInit;
    expect(JSON.parse(String(init.body))).toEqual({ focus: null });
    await waitFor(() => expect(refreshMock).toHaveBeenCalled());
    expect(replaceMock).not.toHaveBeenCalled();
  });

  it("surfaces a friendly error when the API rejects", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(
      new Response("{}", { status: 403 }),
    );

    render(<GeneratePlanForm teamId="team-3" />);
    fireEvent.click(screen.getByRole("button", { name: /generate plan/i }));

    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent(/access/i),
    );
    expect(replaceMock).not.toHaveBeenCalled();
  });
});
