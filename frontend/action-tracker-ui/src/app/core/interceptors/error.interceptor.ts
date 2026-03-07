import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ToastService } from '../services/toast.service';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toastService = inject(ToastService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      switch (error.status) {
        case 400: {
          const body = error.error;
          if (body?.errors && Array.isArray(body.errors) && body.errors.length > 0) {
            body.errors.forEach((msg: string) => toastService.error(msg));
          } else if (body?.message) {
            toastService.error(body.message);
          } else {
            toastService.error('Invalid request.');
          }
          break;
        }
        case 401:
          // Handled by refresh-token interceptor
          break;
        case 403:
          toastService.error('Access denied.');
          break;
        case 404:
          toastService.error('Resource not found.');
          break;
        case 500:
          toastService.error('Server error. Please try again.');
          break;
        default:
          if (error.status > 0) {
            toastService.error(`Unexpected error (${error.status}).`);
          }
      }

      return throwError(() => error);
    })
  );
};
