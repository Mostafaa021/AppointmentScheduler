import { Routes } from '@angular/router';
import { authGuard, doctorOnlyMatch } from './core/auth/auth.guards';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'schedule' },
  {
    path: 'login',
    title: 'Sign in · Appointment Scheduler',
    loadComponent: () => import('./features/auth/login-page').then((m) => m.LoginPage),
  },
  {
    // AC-004-17: Doctor-only users get the read-only view; everyone else falls through to the full page.
    path: 'schedule',
    title: 'My schedule · Appointment Scheduler',
    canMatch: [doctorOnlyMatch],
    loadComponent: () => import('./features/schedule/doctor-schedule-page').then((m) => m.DoctorSchedulePage),
  },
  {
    path: 'schedule',
    title: 'Schedule · Appointment Scheduler',
    canActivate: [authGuard],
    loadComponent: () => import('./features/schedule/schedule-page').then((m) => m.SchedulePage),
  },
  { path: '**', redirectTo: 'schedule' },
];
