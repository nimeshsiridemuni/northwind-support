import { clearAuthSession, getAuthSession } from "../features/auth/authSession";
import type { ProblemDetails } from "../types/api";

const baseUrl = import.meta.env.VITE_API_BASE_URL ?? "/api";

export class ApiError extends Error {
  readonly status: number;
  readonly problem?: ProblemDetails;

  constructor(status: number, message: string, problem?: ProblemDetails) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.problem = problem;
  }

  get fieldErrors(): Record<string, string[]> {
    return this.problem?.errors ?? {};
  }
}

export async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const isLogin = path === "/auth/login";
  const token = isLogin ? undefined : getAuthSession()?.accessToken;

  const headers = new Headers(init?.headers);

  if (!headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  const response = await fetch(`${baseUrl}${path}`, {
    ...init,
    headers,
  });

  if (!response.ok) {
    if (
      response.status === 401 &&
      !isLogin &&
      token &&
      getAuthSession()?.accessToken === token
    ) {
      clearAuthSession();
    }

    const problem = await readProblem(response);

    const fallback =
      response.status === 401
        ? isLogin
          ? "Invalid email or password."
          : "Your session has expired. Please sign in again."
        : response.statusText;

    throw new ApiError(
      response.status,
      problem?.detail ?? problem?.title ?? fallback,
      problem,
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export function toErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof ApiError) {
    return error.message;
  }

  if (error instanceof Error) {
    return `${fallback} (${error.message})`;
  }

  return fallback;
}

async function readProblem(
  response: Response,
): Promise<ProblemDetails | undefined> {
  try {
    const body = (await response.json()) as ProblemDetails & {
      message?: string;
    };

    if (!body || typeof body !== "object") {
      return undefined;
    }

    return {
      ...body,
      detail: body.detail ?? body.message,
    };
  } catch {
    return undefined;
  }
}
