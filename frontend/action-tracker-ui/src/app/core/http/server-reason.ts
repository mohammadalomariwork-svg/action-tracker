import { HttpErrorResponse } from '@angular/common/http';

/**
 * The server's reason for a failed request, and the hand-off between the error
 * interceptor's toast and a screen that shows the reason itself.
 *
 * The interceptor (`core/interceptors/error.interceptor.ts`) toasts one task
 * after the error, and stays silent for an error that a screen passed to
 * `showErrorHere` before then. A screen that forgets to call it gets an extra
 * toast, never a missing message.
 */

const shownHere = new WeakSet<object>();

/**
 * The server's reason, or null when the reply carries none: `message` (the
 * `ApiResponse` body), else `errors` (a list, or the FluentValidation
 * dictionary — joined with a space, repeats dropped). Only for a 400 does the
 * ProblemDetails `detail` come last, as the interceptor's 400 toast has always
 * shown it; on other statuses it can be a raw exception text and is not used.
 */
export function serverReason(err: unknown): string | null {
  const response = err as Partial<HttpErrorResponse> | null | undefined;
  const body = response?.error;
  if (!body || typeof body !== 'object') return null;
  if (typeof body.message === 'string' && body.message.trim()) return body.message;
  const errors = errorList(body.errors);
  if (errors.length) return errors.join(' ');
  if (response?.status === 400 && typeof body.detail === 'string' && body.detail.trim()) return body.detail;
  return null;
}

/**
 * For an error handler that shows the server's reason on the page (an alert, a
 * drawer, a dialog): returns the reason — null means "use the screen's own
 * text" — and tells the error interceptor not to toast this error.
 * Call it in the error handler itself (an `await` before it is fine; a timer
 * or a wait for other requests is too late).
 */
export function showErrorHere(err: unknown): string | null {
  if (err && typeof err === 'object') shownHere.add(err);
  return serverReason(err);
}

/** Whether a screen took this error over with `showErrorHere` (read by the error interceptor). */
export function isShownHere(err: unknown): boolean {
  return !!err && typeof err === 'object' && shownHere.has(err);
}

function errorList(errors: unknown): string[] {
  if (!errors || typeof errors !== 'object') return [];
  const values: unknown[] = Array.isArray(errors) ? errors : Object.values(errors).flat();
  return [...new Set(values.filter((v): v is string => typeof v === 'string' && v.trim() !== ''))];
}
