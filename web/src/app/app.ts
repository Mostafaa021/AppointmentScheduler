import { Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, MatToolbarModule, MatIconModule],
  template: `
    <mat-toolbar color="primary" class="app-toolbar">
      <mat-icon aria-hidden="true">event_available</mat-icon>
      <span class="app-title">Clinic Appointment Scheduler</span>
    </mat-toolbar>
    <main class="app-main">
      <router-outlet />
    </main>
  `,
  styles: `
    .app-toolbar { gap: 12px; background: var(--mat-sys-primary); color: var(--mat-sys-on-primary); }
    .app-title { font: var(--mat-sys-title-large); }
    .app-main { max-width: 1200px; margin: 0 auto; padding: 24px 16px; }
  `,
})
export class App {}
