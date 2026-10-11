import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { App } from "../../App";
import { request } from "../../api/client";
import {
  clearAuthSession,
  getAuthSession,
  setAuthSession,
  type AuthSession,
} from "./authSession";
import { LoginPage } from "./LoginPage";
import { ProtectedRoute } from "./ProtectedRoute";

const fetchMock = vi.fn();

function makeSession(lifetimeMilliseconds = 60_000): AuthSession {
  return {
    accessToken: "test-access-token",
    expiresAtUtc: new Date(Date.now() + lifetimeMilliseconds).toISOString(),
    agent: {
      id: 1,
      fullName: "Alex Turner",
      email: "alex.turner@northwind-support.example",
    },
  };
}

function TicketPage() {
  const location = useLocation();

  return (
    <section>
      <h1>Ticket detail</h1>
      <p>{location.pathname + location.search}</p>

      <button
        type="button"
        onClick={() => {
          void request("/tickets/12").catch(() => undefined);
        }}
      >
        Load ticket
      </button>
    </section>
  );
}

function renderApp(initialPath = "/tickets/12?tab=history") {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />

        <Route element={<ProtectedRoute />}>
          <Route path="/" element={<App />}>
            <Route path="tickets/:id" element={<TicketPage />} />
            <Route path="tickets" element={<TicketPage />} />
          </Route>
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

async function showLogin() {
  renderApp();

  await screen.findByRole("heading", {
    name: "Sign in",
  });
}

function fillCredentials(password = "Local_Agent_Password1") {
  fireEvent.change(screen.getByLabelText("Email"), {
    target: {
      value: "alex.turner@northwind-support.example",
    },
  });

  fireEvent.change(screen.getByLabelText("Password"), {
    target: { value: password },
  });
}

describe("authentication flow", () => {
  beforeEach(() => {
    clearAuthSession();
    fetchMock.mockReset();
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => {
    cleanup();
    clearAuthSession();
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it("redirects an anonymous visitor to login", async () => {
    await showLogin();

    expect(
      screen.queryByRole("heading", { name: "Ticket detail" }),
    ).not.toBeInTheDocument();

    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("validates the form before sending a login request", async () => {
    await showLogin();

    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));

    expect(screen.getByRole("alert")).toHaveTextContent(
      "Enter a valid email address.",
    );

    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("shows invalid credentials without creating a session", async () => {
    fetchMock.mockResolvedValue(
      new Response(
        JSON.stringify({
          message: "Invalid email or password.",
        }),
        {
          status: 401,
          headers: { "Content-Type": "application/json" },
        },
      ),
    );

    await showLogin();
    fillCredentials("wrong-password");

    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Invalid email or password.",
    );

    expect(getAuthSession()).toBeNull();

    expect(screen.getByRole("button", { name: "Sign in" })).toBeEnabled();
  });

  it("returns to the requested page after successful login", async () => {
    const session = makeSession();

    fetchMock.mockResolvedValue(
      new Response(JSON.stringify(session), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    );

    await showLogin();
    fillCredentials();

    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));

    await screen.findByRole("heading", {
      name: "Ticket detail",
    });

    expect(screen.getByText("/tickets/12?tab=history")).toBeInTheDocument();

    expect(screen.getByText("Alex Turner")).toBeInTheDocument();

    expect(getAuthSession()?.accessToken).toBe(session.accessToken);

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("clears the session and redirects when the user signs out", async () => {
    setAuthSession(makeSession());
    renderApp();

    fireEvent.click(screen.getByRole("button", { name: "Sign out" }));

    await screen.findByRole("heading", { name: "Sign in" });

    expect(getAuthSession()).toBeNull();

    expect(window.sessionStorage.getItem("northwind.auth")).toBeNull();
  });

  it("attaches the token and clears the session after a 401", async () => {
    setAuthSession(makeSession());

    fetchMock.mockResolvedValue(new Response(null, { status: 401 }));

    renderApp();

    fireEvent.click(screen.getByRole("button", { name: "Load ticket" }));

    await screen.findByRole("heading", { name: "Sign in" });

    await waitFor(() => {
      expect(getAuthSession()).toBeNull();
    });

    const options = fetchMock.mock.calls[0][1] as RequestInit;

    expect(new Headers(options.headers).get("Authorization")).toBe(
      "Bearer test-access-token",
    );
  });

  it("returns to login when the token expires", async () => {
    vi.useFakeTimers();
    setAuthSession(makeSession(1_000));
    renderApp();

    expect(
      screen.getByRole("heading", { name: "Ticket detail" }),
    ).toBeInTheDocument();

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1_001);
    });

    expect(getAuthSession()).toBeNull();

    expect(
      screen.getByRole("heading", { name: "Sign in" }),
    ).toBeInTheDocument();
  });
});
