import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { TeamCreateForm } from "@/components/TeamCreateForm";

const replaceMock = vi.fn();
const refreshMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: replaceMock, refresh: refreshMock }),
}));

describe("TeamCreateForm", () => {
  beforeEach(() => {
    replaceMock.mockReset();
    refreshMock.mockReset();
    vi.stubGlobal("fetch", vi.fn());
  });
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("auto-derives code from name until coach edits the code", () => {
    render(<TeamCreateForm />);
    const name = screen.getByLabelText(/team name/i) as HTMLInputElement;
    const code = screen.getByLabelText(/short code/i) as HTMLInputElement;

    fireEvent.change(name, { target: { value: "Riverside Roar" } });
    expect(code.value).toBe("riverside-roar");

    fireEvent.change(code, { target: { value: "rr-u16" } });
    fireEvent.change(name, { target: { value: "Riverside Roar U16" } });
    // Code stays as the coach edited it.
    expect(code.value).toBe("rr-u16");
  });

  it("posts to /teams and redirects to the new team page on success", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ id: "team-1", name: "X", code: "x" }), {
        status: 201,
        headers: { "content-type": "application/json" },
      }),
    );

    render(<TeamCreateForm />);
    fireEvent.change(screen.getByLabelText(/team name/i), {
      target: { value: "Riverside Roar" },
    });
    fireEvent.submit(screen.getByRole("form", { name: /create team/i }));

    await waitFor(() =>
      expect(replaceMock).toHaveBeenCalledWith("/teams/team-1"),
    );
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("/api/proxy/teams");
    expect((init as RequestInit).method).toBe("POST");
    expect(JSON.parse((init as RequestInit).body as string)).toEqual({
      name: "Riverside Roar",
      code: "riverside-roar",
    });
  });

  it("shows a friendly message on duplicate code (409)", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(new Response("{}", { status: 409 }));

    render(<TeamCreateForm />);
    fireEvent.change(screen.getByLabelText(/team name/i), {
      target: { value: "Roar" },
    });
    fireEvent.submit(screen.getByRole("form", { name: /create team/i }));

    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent(/already exists/),
    );
    expect(replaceMock).not.toHaveBeenCalled();
  });
});
