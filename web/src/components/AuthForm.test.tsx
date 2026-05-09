import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { AuthForm } from "@/components/AuthForm";

const replaceMock = vi.fn();
const refreshMock = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: replaceMock, refresh: refreshMock }),
}));

describe("AuthForm", () => {
  beforeEach(() => {
    replaceMock.mockReset();
    refreshMock.mockReset();
    vi.stubGlobal("fetch", vi.fn());
  });
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("login: posts credentials and redirects on success", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(new Response("{}", { status: 200 }));

    render(<AuthForm mode="login" />);
    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: "c@x.io" } });
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: "swordfish" } });
    fireEvent.submit(screen.getByRole("form", { name: /sign in/i }));

    await waitFor(() => expect(replaceMock).toHaveBeenCalledWith("/dashboard"));
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("/api/proxy/auth/login");
    expect((init as RequestInit).method).toBe("POST");
    expect(JSON.parse((init as RequestInit).body as string)).toEqual({
      email: "c@x.io",
      password: "swordfish",
    });
  });

  it("login: shows friendly error on 401 and does not redirect", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(new Response("{}", { status: 401 }));

    render(<AuthForm mode="login" />);
    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: "c@x.io" } });
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: "wrongpass" } });
    fireEvent.submit(screen.getByRole("form", { name: /sign in/i }));

    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent(/incorrect/i),
    );
    expect(replaceMock).not.toHaveBeenCalled();
  });

  it("register: blocks short passwords client-side without calling fetch", async () => {
    const fetchMock = vi.mocked(global.fetch);

    render(<AuthForm mode="register" />);
    fireEvent.change(screen.getByLabelText(/your name/i), { target: { value: "Coach" } });
    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: "c@x.io" } });
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: "short" } });
    fireEvent.submit(screen.getByRole("form", { name: /create account/i }));

    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent(/12 characters/),
    );
    expect(fetchMock).not.toHaveBeenCalled();
    expect(replaceMock).not.toHaveBeenCalled();
  });

  it("register: posts displayName + credentials on success", async () => {
    const fetchMock = vi.mocked(global.fetch);
    fetchMock.mockResolvedValueOnce(new Response("{}", { status: 201 }));

    render(<AuthForm mode="register" />);
    fireEvent.change(screen.getByLabelText(/your name/i), { target: { value: "Coach Sam" } });
    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: "sam@x.io" } });
    fireEvent.change(screen.getByLabelText(/password/i), {
      target: { value: "correct horse battery staple" },
    });
    fireEvent.submit(screen.getByRole("form", { name: /create account/i }));

    await waitFor(() => expect(replaceMock).toHaveBeenCalledWith("/dashboard"));
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("/api/proxy/auth/register");
    expect(JSON.parse((init as RequestInit).body as string)).toEqual({
      email: "sam@x.io",
      password: "correct horse battery staple",
      displayName: "Coach Sam",
    });
  });
});
