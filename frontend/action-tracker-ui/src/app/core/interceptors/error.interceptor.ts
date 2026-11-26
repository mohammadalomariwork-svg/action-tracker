import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ToastService } from '../services/toast.service';
import { isShownHere, serverReason } from '../http/server-reason';

/**
 * Toasts a failed request once. The toast waits one task, so a screen that
 * shows the reason itself can call `showErrorHere(err)` in its error handler
 * and keep the toast away (see `core/http/server-reason.ts`).
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toastService = inject(ToastService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      const messages = errorToastMessages(error);
      if (messages.length) {
        setTimeout(() => {
          if (!isShownHere(error)) messages.forEach(m => toastService.error(m));
        });
      }
      return throwError(() => error);
    })
  );
};

/**
 * The toast text for a failed request ([] = no toast). 403, 409, 422, 503 and
 * any other uncommon status give the server's reason (`message`, else `errors`,
 * never the ProblemDetails `detail`) and the generic text only when there is
 * none. 400, 404 and 500 are unchanged.
 */
export function errorToastMessages(error: HttpErrorResponse): string[] {
  switch (error.status) {
    case 400: {
      const body = error.error;
      if (body?.errors && Array.isArray(body.errors) && body.errors.length > 0) return [...body.errors];
      if (body?.message) return [body.message];
      if (body?.detail) return [body.detail];
      return ['Invalid request.'];
    }
    case 401:
      // Handled by the auth interceptor (token refresh).
      return [];
    case 403:
      return [serverReason(error) ?? 'Access denied.'];
    case 404:
      return ['Resource not found.'];
    case 500:
      return ['Server error. Please try again.'];
    default:
      return error.status > 0 ? [serverReason(error) ?? `Unexpected error (${error.status}).`] : [];
  }
}
