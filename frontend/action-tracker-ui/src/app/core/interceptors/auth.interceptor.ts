import { HttpInterceptorFn, HttpRequest, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { BehaviorSubject, throwError } from 'rxjs';
import { catchError, filter, switchMap, take } from 'rxjs/operators';
import { AuthService } from '../services/auth.service';
import { PermissionStateService } from '../../features/permissions/services/permission-state.service';

// Paths that must never have a token attached and must never trigger a
// refresh attempt (avoids loops and redundant headers on public endpoints).
const SKIP_URLS = [
  '/auth/login',
  '/auth/azure-login',
  '/auth/refresh-token',
];

// ── Module-level refresh state ────────────────────────────────────────────────
// Shared across all interceptor invocations within the same app instance so that
// exactly one refresh call is ever in-flight at a time.
const isRefreshing$ = new BehaviorSubject<boolean>(false);

function withBearer(req: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService     = inject(AuthService);
  const permissionState = inject(PermissionStateService);

  // Skip public auth endpoints entirely — no token injection, no 401 handling.
  if (SKIP_URLS.some(url => req.url.includes(url))) {
    return next(req);
  }

  // Attach the current access token when available.
  const token = authService.getAccessToken();
  const outgoing = token ? withBearer(req, token) : req;

  return next(outgoing).pipe(
    catchError((error: HttpErrorResponse) => {
      // Only intercept 401 Unauthorized — let everything else pass through.
      if (error.status !== 401) {
        return throwError(() => error);
      }

      // ── Another refresh is already in progress ────────────────────────────
      // Queue this request: wait for isRefreshing$ to go false, then retry
      // using whatever token the refresh stored in localStorage.
      if (isRefreshing$.getValue()) {
        return isRefreshing$.pipe(
          filter(refreshing => !refreshing),
          take(1),
          switchMap(() => {
            const fresh = authService.getAccessToken();
            return next(fresh ? withBearer(req, fresh) : req);
          }),
        );
      }

      // ── Kick off the refresh ──────────────────────────────────────────────
      isRefreshing$.next(true);

      return authService.refreshToken().pipe(
        switchMap(response => {
          isRefreshing$.next(false);
          // Reload permissions outside the current HTTP chain to avoid circular
          // dependency (PermissionStateService → HttpClient → authInterceptor).
          queueMicrotask(() => permissionState.loadPermissions().subscribe());
          // Retry the original request with the new token.
          return next(withBearer(req, response.accessToken));
        }),
        catchError(refreshError => {
          isRefreshing$.next(false);
          // Refresh failed — clear session and redirect to login.
          authService.logout();
          return throwError(() => refreshError);
        }),
      );
    }),
  );
};
