import { Link, NavLink, Outlet, useNavigate } from "react-router-dom";
import { clearAuthSession } from "./features/auth/authSession";
import { useAuthSession } from "./features/auth/useAuthSession";

export function App() {
  const session = useAuthSession();
  const navigate = useNavigate();

  function handleLogout() {
    clearAuthSession();
    void navigate("/login", { replace: true });
  }

  return (
    <div className="app">
      <header className="app__header">
        <Link className="app__brand" to="/tickets">
          Northwind Support
        </Link>

        <nav className="app__nav" aria-label="Main">
          <NavLink to="/tickets">Tickets</NavLink>
          <NavLink to="/customers">Customers</NavLink>
        </nav>

        <div className="app__account">
          <span className="app__agent-name">{session?.agent.fullName}</span>

          <button
            type="button"
            className="button button--ghost"
            onClick={handleLogout}
          >
            Sign out
          </button>
        </div>
      </header>

      <main className="app__main">
        <Outlet />
      </main>
    </div>
  );
}
