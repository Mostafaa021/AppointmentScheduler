import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MAT_ICON_DEFAULT_OPTIONS } from '@angular/material/icon';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { AuthService } from './core/auth/auth.service';
import { apiErrorInterceptor } from './core/http/api-error.interceptor';
import { apiResponseInterceptor } from './core/http/api-response.interceptor';
import { idempotencyKeyInterceptor } from './core/http/idempotency-key.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    // Errors outermost: a 401 the auth interceptor recovers from never reaches the snackbar (spec 004 design).
    // Envelope unwrapping innermost, so everything above it sees DTOs (ADR-0015).
    // Idempotency keys outside auth: the retry after a token refresh resends the same key (spec 009).
    provideHttpClient(withInterceptors([apiErrorInterceptor, idempotencyKeyInterceptor, authInterceptor, apiResponseInterceptor])),
    // AC-004-16: one silent POST /auth/refresh before the first navigation.
    provideAppInitializer(() => inject(AuthService).restore()),
    provideNativeDateAdapter(),
    { provide: MAT_ICON_DEFAULT_OPTIONS, useValue: { fontSet: 'material-symbols-outlined' } },
  ],
};
