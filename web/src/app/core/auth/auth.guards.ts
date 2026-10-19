import { inject } from '@angular/core';
import { CanActivateFn, CanMatchFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** AC-004-16: signed in, or go to /login. Synchronous because the app initializer already tried the silent refresh. */
export const authGuard: CanActivateFn = () => inject(AuthService).isAuthenticated() || inject(Router).parseUrl('/login');

/** AC-004-17: matches only for users whose only role is Doctor (implies signed in). */
export const doctorOnlyMatch: CanMatchFn = () => inject(AuthService).isDoctorOnly();
