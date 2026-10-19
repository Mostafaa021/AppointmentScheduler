import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'schedule' },
  {
    path: 'schedule',
    title: 'Schedule · Appointment Scheduler',
    loadComponent: () => import('./features/schedule/schedule-page').then((m) => m.SchedulePage),
  },
  { path: '**', redirectTo: 'schedule' },
];
