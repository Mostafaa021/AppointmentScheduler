import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, firstValueFrom, map, of, share } from 'rxjs';
import { AuthResponse, AuthUser } from '../api/models';
import { SILENT } from '../http/api-error.interceptor';

/** Spec 004 (S-22): session state. The refresh token is an HttpOnly cookie the browser handles; JS never sees it. */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly base = '/api/v1/auth';
  // AC-004-21: the access token lives only in this signal — never in Web Storage, IndexedDB or a JS-readable cookie.
  private readonly token = signal<string | null>(null);
  private readonly current = signal<AuthUser | null>(null);
  private refreshing: Observable<string> | null = null;

  readonly user = this.current.asReadonly();
  readonly isAuthenticated = computed(() => this.token() !== null);
  readonly isDoctorOnly = computed(() => {
    const roles = this.current()?.roles ?? [];
    return roles.length === 1 && roles[0] === 'Doctor';
  });

  accessToken(): string | null {
    return this.token();
  }

  login(email: string, password: string): Observable<AuthUser> {
    return this.http.post<AuthResponse>(`${this.base}/login`, { email, password }).pipe(map((r) => this.accept(r)));
  }

  /** Single-flight (AC-004-19): concurrent callers share one `POST /auth/refresh`; the next call after it settles starts a new one. */
  refresh(): Observable<string> {
    this.refreshing ??= this.http.post<AuthResponse>(`${this.base}/refresh`, null, this.cookieCall()).pipe(
      map((r) => {
        this.accept(r);
        return r.accessToken;
      }),
      finalize(() => (this.refreshing = null)),
      share(),
    );
    return this.refreshing;
  }

  /** App start (AC-004-16): one silent refresh; resolves either way so guards can decide synchronously. */
  restore(): Promise<void> {
    return firstValueFrom(this.refresh().pipe(map(() => undefined), catchError(() => of(undefined))));
  }

  /** AC-004-20: clear the session and go to /login even if the logout call fails. */
  logout(): void {
    this.http
      .post<void>(`${this.base}/logout`, null, this.cookieCall())
      .pipe(
        finalize(() => {
          this.clear();
          void this.router.navigateByUrl('/login');
        }),
      )
      .subscribe({ error: () => undefined });
  }

  clear(): void {
    this.token.set(null);
    this.current.set(null);
  }

  private accept(response: AuthResponse): AuthUser {
    this.token.set(response.accessToken);
    this.current.set(response.user);
    return response.user;
  }

  private cookieCall() {
    return { withCredentials: true, context: new HttpContext().set(SILENT, true) };
  }
}
