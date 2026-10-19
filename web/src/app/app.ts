import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, MatToolbarModule, MatIconModule, MatButtonModule],
  template: `
    <mat-toolbar color="primary" class="app-toolbar">
      <mat-icon aria-hidden="true">event_available</mat-icon>
      <span class="app-title">Clinic Appointment Scheduler</span>
      @if (auth.user(); as user) {
        <span class="spacer"></span>
        <span class="app-user">{{ user.email }} · {{ user.roles.join(', ') }}</span>
        <button mat-button class="logout" (click)="auth.logout()">
          <mat-icon>logout</mat-icon>
          Log out
        </button>
      }
    </mat-toolbar>
    <main class="app-main">
      <router-outlet />
    </main>
  `,
  styles: `
    .app-toolbar { gap: 12px; background: var(--mat-sys-primary); color: var(--mat-sys-on-primary); }
    .app-title { font: var(--mat-sys-title-large); }
    .spacer { flex: 1; }
    .app-user { font: var(--mat-sys-body-medium); }
    .logout { color: inherit; }
    .app-main { max-width: 1200px; margin: 0 auto; padding: 24px 16px; }
  `,
})
export class App {
  protected readonly auth = inject(AuthService);
}
