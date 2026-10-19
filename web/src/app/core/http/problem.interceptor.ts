import { HttpContextToken, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { catchError, throwError } from 'rxjs';
import { Problem } from '../api/models';

/** Set on a request to suppress the snackbar (silent startup refresh, logout). */
export const SILENT = new HttpContextToken<boolean>(() => false);

/** Shows the server's ProblemDetails message for any failed request, then lets callers react (e.g. refresh on 409). */
export const problemInterceptor: HttpInterceptorFn = (req, next) => {
  const snackBar = inject(MatSnackBar);
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (!req.context.get(SILENT)) snackBar.open(problemMessage(error), 'Dismiss', { duration: 6000 });
      return throwError(() => error);
    }),
  );
};

export function problemMessage(error: HttpErrorResponse): string {
  if (error.status === 0) return 'Cannot reach the server. Check your connection and try again.';
  const problem = error.error as Problem | null;
  const fieldErrors = problem?.errors ? Object.values(problem.errors).flat().join(' ') : '';
  return fieldErrors || problem?.detail || problem?.title || `Request failed (${error.status}).`;
}
