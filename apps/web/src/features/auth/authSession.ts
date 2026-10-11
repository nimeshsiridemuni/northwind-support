export interface AuthenticatedAgent {
  id: number;
  fullName: string;
  email: string;
}

export interface AuthSession {
  accessToken: string;
  expiresAtUtc: string;
  agent: AuthenticatedAgent;
}

const storageKey = "northwind.auth";
const listeners = new Set<() => void>();

let currentSession: AuthSession | null = readStoredSession();
let expiryTimer: number | undefined;

function readStoredSession(): AuthSession | null {
  if (typeof window === "undefined") {
    return null;
  }

  try {
    const stored = window.sessionStorage.getItem(storageKey);

    if (!stored) {
      return null;
    }

    const session = JSON.parse(stored) as AuthSession;
    const expiry = Date.parse(session.expiresAtUtc);

    if (
      typeof session.accessToken !== "string" ||
      !session.accessToken ||
      !Number.isFinite(expiry) ||
      expiry <= Date.now() ||
      !Number.isInteger(session.agent?.id) ||
      typeof session.agent?.fullName !== "string" ||
      typeof session.agent?.email !== "string"
    ) {
      window.sessionStorage.removeItem(storageKey);
      return null;
    }

    return session;
  } catch {
    return null;
  }
}

export function getAuthSession(): AuthSession | null {
  return currentSession;
}

export function setAuthSession(session: AuthSession): void {
  const expiry = Date.parse(session.expiresAtUtc);

  if (!Number.isFinite(expiry) || expiry <= Date.now()) {
    throw new Error("The login session has expired. Please sign in again.");
  }

  currentSession = session;

  try {
    window.sessionStorage.setItem(storageKey, JSON.stringify(session));
  } catch {
    // If storage is unavailable, keep the session in memory.
  }

  scheduleExpiry();
  notifyListeners();
}

export function clearAuthSession(): void {
  currentSession = null;

  if (expiryTimer !== undefined) {
    window.clearTimeout(expiryTimer);
    expiryTimer = undefined;
  }

  try {
    window.sessionStorage.removeItem(storageKey);
  } catch {
    // Clearing the in-memory session still logs the user out.
  }

  notifyListeners();
}

export function subscribeToAuthSession(listener: () => void): () => void {
  listeners.add(listener);

  return () => {
    listeners.delete(listener);
  };
}

function notifyListeners(): void {
  for (const listener of listeners) {
    listener();
  }
}

function scheduleExpiry(): void {
  if (typeof window === "undefined") {
    return;
  }

  if (expiryTimer !== undefined) {
    window.clearTimeout(expiryTimer);
    expiryTimer = undefined;
  }

  if (!currentSession) {
    return;
  }

  const remaining = Date.parse(currentSession.expiresAtUtc) - Date.now();

  if (!Number.isFinite(remaining) || remaining <= 0) {
    clearAuthSession();
    return;
  }

  expiryTimer = window.setTimeout(
    scheduleExpiry,
    Math.min(remaining, 2_147_483_647),
  );
}

scheduleExpiry();
