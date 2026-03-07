import { HttpInterceptorFn, HttpRequest, HttpHandlerFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { BehaviorSubject, Observable, throwError } from 'rxjs';
import { catchError, filter, switchMap, take } from 'rxjs/operators';
import { AuthService } from '../services/auth.service';

// Module-level state shared across calls within the same app instance
let isRefreshing = false;
const refreshTokenSubject = new BehaviorSubject<string | null>(null);

export const refreshTokenInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status !== 401) {
        return throwError(() => error);
      }

      if (isRefreshing) {
        // Queue this request until the token has been refreshed
        return refreshTokenSubject.pipe(
          filter(token => token !== null),
          take(1),
          switchMap(token =>
            next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }))
          )
        );
      }

      isRefreshing = true;
      refreshTokenSubject.next(null);

      return authService.refreshToken().pipe(
        switchMap(response => {
          isRefreshing = false;
          refreshTokenSubject.next(response.accessToken);
          return next(
            req.clone({ setHeaders: { Authorization: `Bearer ${response.accessToken}` } })
          );
        }),
        catchError(refreshError => {
          isRefreshing = false;
          refreshTokenSubject.next(null);
          authService.logout();
          return throwError(() => refreshError);
        })
      );
    })
  );
};
