import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, of, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

const anonymous = ['/api/v1/auth/login', '/api/v1/auth/refresh'];

const is401 = (error: unknown) => error instanceof HttpErrorResponse && error.status === 401;

/**
 * AC-004-18: Bearer only on relative `/api/v1/` URLs (never another origin), except login/refresh.
 * AC-004-19/20: on 401, one shared refresh then a single retry; refresh failure or a second 401 ends the session.
 * The retry goes straight to `next`, so it never re-enters this interceptor (no second refresh).
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const path = req.url.split('?')[0];
  if (!path.startsWith('/api/v1/') || anonymous.includes(path)) return next(req);

  const auth = inject(AuthService);
  const router = inject(Router);
  const sent = auth.accessToken();
  const withToken = (token: string | null) =>
    token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
  const endSession = (error: unknown) => {
    auth.clear();
    void router.navigateByUrl('/login');
    return throwError(() => error);
  };

  return next(withToken(sent)).pipe(
    catchError((error: unknown) => {
      if (!is401(error)) return throwError(() => error);
      const current = auth.accessToken();
      // A refresh already finished while this request was in flight: reuse its token instead of refreshing again.
      const token$ = current && current !== sent ? of(current) : auth.refresh();
      return token$.pipe(
        catchError(() => endSession(error)),
        switchMap((token) =>
          next(withToken(token)).pipe(
            catchError((retryError: unknown) => (is401(retryError) ? endSession(retryError) : throwError(() => retryError))),
          ),
        ),
      );
    }),
  );
};
