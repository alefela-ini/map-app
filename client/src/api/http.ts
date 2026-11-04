/**
 * Thin fetch wrapper that turns non-2xx responses (RFC 7807 problem details) into {@link ApiError}.
 */

/** Base URL of the API. Empty = same origin (Vite dev proxy / nginx forward `/api` to the server). */
const API_BASE_URL: string = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');

export class ApiError extends Error {
  readonly status: number;
  /** Field errors keyed by path, e.g. `[1].geometry.coordinates`. */
  readonly fieldErrors: Record<string, string[]>;

  constructor(message: string, status: number, fieldErrors: Record<string, string[]> = {}) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.fieldErrors = fieldErrors;
  }
}

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

/**
 * Upper bound for one request. Without it a connection that hangs would keep `busy` (and the disabled buttons)
 * on until the browser gives up, which can take minutes. A timed-out Save may still have been stored on the
 * server; retrying it is safe because every item carries its idempotency key (`clientId`).
 */
const REQUEST_TIMEOUT_MS = 15_000;

export async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set('Accept', 'application/json');
  if (init.body !== undefined) headers.set('Content-Type', 'application/json');

  const timeout = AbortSignal.timeout(REQUEST_TIMEOUT_MS);
  const signal = init.signal ? AbortSignal.any([init.signal, timeout]) : timeout;

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, { ...init, headers, signal });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'TimeoutError') {
      throw new ApiError('The server did not respond in time. Please try again.', 0);
    }
    if (error instanceof DOMException && error.name === 'AbortError') throw error;
    throw new ApiError('Cannot reach the server. Is the API running?', 0);
  }

  if (!response.ok) {
    if (response.status === 502 || response.status === 503 || response.status === 504) {
      const problem = await readProblem(response);
      throw new ApiError(problem?.title ?? 'The server is unavailable. Please try again later.', response.status);
    }

    const problem = await readProblem(response);
    throw new ApiError(
      problem?.title ?? problem?.detail ?? `Request failed with status ${response.status}.`,
      response.status,
      problem?.errors ?? {},
    );
  }

  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

async function readProblem(response: Response): Promise<ProblemDetails | null> {
  const contentType = response.headers.get('Content-Type') ?? '';
  if (!contentType.includes('json')) return null;
  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    return null;
  }
}

/**
 * Builds a user-facing message. Field errors such as `[1].geometry.coordinates` are prefixed with
 * the name of the item at that index, so the user knows which draft is wrong.
 */
export function describeError(error: unknown, itemNames: readonly string[] = []): string {
  if (!(error instanceof ApiError)) {
    return error instanceof Error ? error.message : 'Unexpected error.';
  }

  const details = Object.entries(error.fieldErrors).flatMap(([path, messages]) => {
    const index = /^\[(\d+)\]/.exec(path)?.[1];
    const prefix = index !== undefined && itemNames[Number(index)] ? `${itemNames[Number(index)]}: ` : '';
    return messages.map((message) => `${prefix}${message}`);
  });

  return details.length > 0 ? details.join(' ') : error.message;
}
