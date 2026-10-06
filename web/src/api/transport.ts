// Thin fetch wrapper used by the generated client (see orval.config.ts). It owns transport concerns only
// (errors, CSRF header); request/response shapes always come from the generated models. It must not import from
// ./generated: Orval bundles this file while that folder is being regenerated.
export class ApiError extends Error {
  readonly status: number;
  readonly detail?: string;

  constructor(status: number, title: string, detail?: string) {
    super(title);
    this.name = "ApiError";
    this.status = status;
    this.detail = detail;
  }
}

async function toApiError(response: Response): Promise<ApiError> {
  let title = response.statusText || `HTTP ${response.status}`;
  let detail: string | undefined;
  try {
    const problem = (await response.json()) as {
      title?: string;
      detail?: string;
    };
    title = problem.title ?? title;
    detail = problem.detail;
  } catch {
    // Empty or non-JSON error body.
  }
  return new ApiError(response.status, title, detail);
}

// The antiforgery token is bound to the signed-in identity, so it is cached only until something goes wrong.
// The app registers how to fetch it (using the generated client) at startup: see index.tsx.
let tokenProvider: (() => Promise<string>) | undefined;
let csrfToken: Promise<string> | undefined;

export function setCsrfTokenProvider(provider: () => Promise<string>) {
  tokenProvider = provider;
  csrfToken = undefined;
}

function getCsrfToken(): Promise<string> {
  if (!tokenProvider)
    return Promise.reject(new Error("No CSRF token provider registered."));
  csrfToken ??= tokenProvider().catch((error: unknown) => {
    csrfToken = undefined;
    throw error;
  });
  return csrfToken;
}

const isUnsafe = (method: string) =>
  !["GET", "HEAD", "OPTIONS", "TRACE"].includes(method);

async function send(url: string, options: RequestInit): Promise<Response> {
  const method = (options.method ?? "GET").toUpperCase();
  const headers = new Headers(options.headers);
  if (isUnsafe(method)) headers.set("X-CSRF-TOKEN", await getCsrfToken());
  return fetch(url, { credentials: "same-origin", ...options, headers });
}

export const apiFetch = async <T>(
  url: string,
  options: RequestInit = {},
): Promise<T> => {
  let response = await send(url, options);

  // A stale token (for example after the session changed) is replaced once, transparently.
  if (
    response.status === 400 &&
    isUnsafe((options.method ?? "GET").toUpperCase())
  ) {
    const problem = await toApiError(response.clone());
    if (problem.message.toLowerCase().includes("antiforgery")) {
      csrfToken = undefined;
      response = await send(url, options);
    }
  }

  if (!response.ok) throw await toApiError(response);
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
};

export type ErrorType<_Error> = ApiError;
