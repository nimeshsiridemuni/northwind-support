import { useState, type FormEvent } from "react";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { request, toErrorMessage } from "../../api/client";
import { setAuthSession, type AuthSession } from "./authSession";
import { useAuthSession } from "./useAuthSession";

export function LoginPage() {
  const session = useAuthSession();
  const location = useLocation();
  const navigate = useNavigate();

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string>();

  const state = location.state as { from?: unknown } | null;
  const requestedDestination = state?.from;

  const destination =
    typeof requestedDestination === "string" &&
    requestedDestination.startsWith("/") &&
    !requestedDestination.startsWith("//") &&
    requestedDestination.split(/[?#]/)[0] !== "/login"
      ? requestedDestination
      : "/tickets";

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (pending) {
      return;
    }

    const normalizedEmail = email.trim();

    if (
      normalizedEmail.length > 256 ||
      !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(normalizedEmail)
    ) {
      setError("Enter a valid email address.");
      return;
    }

    if (!password || password.length > 1024) {
      setError("Enter your password.");
      return;
    }

    setPending(true);
    setError(undefined);

    try {
      const result = await request<AuthSession>("/auth/login", {
        method: "POST",
        body: JSON.stringify({
          email: normalizedEmail,
          password,
        }),
      });

      setAuthSession(result);
      void navigate(destination, { replace: true });
    } catch (failure) {
      setError(toErrorMessage(failure, "Unable to sign in."));
    } finally {
      setPending(false);
    }
  }

  if (session) {
    return <Navigate to={destination} replace />;
  }

  return (
    <main className="app__main">
      <section aria-labelledby="login-heading">
        <header className="page-header">
          <div>
            <h1 id="login-heading">Sign in</h1>
            <p>Sign in to Northwind Support with your agent account.</p>
          </div>
        </header>

        <form
          className="card"
          onSubmit={(event) => void handleSubmit(event)}
          noValidate
          aria-busy={pending}
        >
          {error && (
            <p id="login-error" role="alert">
              {error}
            </p>
          )}

          <div className="field">
            <label htmlFor="login-email">Email</label>
            <input
              id="login-email"
              name="email"
              type="email"
              autoComplete="username"
              maxLength={256}
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              disabled={pending}
              aria-describedby={error ? "login-error" : undefined}
              required
            />
          </div>

          <div className="field">
            <label htmlFor="login-password">Password</label>
            <input
              id="login-password"
              name="password"
              type="password"
              autoComplete="current-password"
              maxLength={1024}
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              disabled={pending}
              aria-describedby={error ? "login-error" : undefined}
              required
            />
          </div>

          <div className="button-row">
            <button
              type="submit"
              className="button button--primary"
              disabled={pending}
            >
              {pending ? "Signing in…" : "Sign in"}
            </button>
          </div>
        </form>
      </section>
    </main>
  );
}
