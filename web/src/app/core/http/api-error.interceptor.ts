import { HttpContextToken, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { catchError, throwError } from 'rxjs';
import { ApiResponse } from '../api/models';

/** Set on a request to suppress the snackbar (silent startup refresh, logout). */
export const SILENT = new HttpContextToken<boolean>(() => false);

/** Shows the server's ApiResponse message for any failed request, then lets callers react on `errorCode` (e.g. refresh on DOCTOR_CONFLICT). */
export const apiErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const snackBar = inject(MatSnackBar);
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (!req.context.get(SILENT)) snackBar.open(errorMessage(error), 'Dismiss', { duration: 6000 });
      return throwError(() => error);
    }),
  );
};

export function errorMessage(error: HttpErrorResponse): string {
  if (error.status === 0) return 'Cannot reach the server. Check your connection and try again.';
  const body = error.error as ApiResponse<unknown> | null;
  const fieldErrors = body?.errors ? Object.values(body.errors).flat().join(' ') : '';
  return fieldErrors || body?.message || `Request failed (${error.status}).`;
}
