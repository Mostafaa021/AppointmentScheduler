import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Appointment } from '../../core/api/models';
import { SchedulerApi, fromIsoDate, toIsoDate } from '../../core/api/scheduler-api';
import { AuthService } from '../../core/auth/auth.service';

/**
 * AC-004-17 (BR-7): read-only day schedule for a Doctor-only user. Only `GET /appointments` for their own doctor —
 * never /doctors, /patients, availability or mutations (all 403 for Doctor). The list template is duplicated from
 * SchedulePage on purpose (design); extract a shared component when a third view needs it.
 */
@Component({
  selector: 'app-doctor-schedule-page',
  imports: [
    FormsModule, MatButtonModule, MatCardModule, MatChipsModule, MatDatepickerModule, MatFormFieldModule, MatIconModule,
    MatInputModule, MatProgressBarModule, MatTooltipModule,
  ],
  template: `
    <section class="filters" aria-label="Choose date">
      <div class="date">
        <button mat-icon-button (click)="shiftDay(-1)" aria-label="Previous day" matTooltip="Previous day">
          <mat-icon>chevron_left</mat-icon>
        </button>
        <mat-form-field appearance="outline">
          <mat-label>Date</mat-label>
          <input matInput [matDatepicker]="picker" [ngModel]="date()" (ngModelChange)="selectDate($event)" />
          <mat-datepicker-toggle matIconSuffix [for]="picker" />
          <mat-datepicker #picker />
        </mat-form-field>
        <button mat-icon-button (click)="shiftDay(1)" aria-label="Next day" matTooltip="Next day">
          <mat-icon>chevron_right</mat-icon>
        </button>
        <button mat-button (click)="goToday()">Today</button>
      </div>
    </section>

    @if (loading()) {
      <mat-progress-bar mode="indeterminate" />
    }

    @if (!doctorId) {
      <p class="hint">Your account is not linked to a doctor.</p>
    } @else {
      <mat-card appearance="outlined" class="single">
        <mat-card-header>
          <mat-card-title>My day schedule</mat-card-title>
          <mat-card-subtitle>{{ appointments().length }} appointment(s)</mat-card-subtitle>
        </mat-card-header>
        <mat-card-content>
          <ul class="appointments">
            @for (a of appointments(); track a.id) {
              <li [class.cancelled]="a.status === 'Cancelled'">
                <div class="when">{{ a.localStart }}–{{ a.localEnd }}</div>
                <div class="who">
                  <strong>{{ a.patientName }}</strong> <span class="mrn">{{ a.patientMrn }}</span>
                  <div class="reason">{{ a.reason }}</div>
                  @if (a.cancellationReason) {
                    <div class="reason">Cancelled: {{ a.cancellationReason }}</div>
                  }
                </div>
                <mat-chip-set><mat-chip [highlighted]="a.status === 'Scheduled'">{{ a.status }}</mat-chip></mat-chip-set>
              </li>
            } @empty {
              <li class="empty">No appointments.</li>
            }
          </ul>
        </mat-card-content>
      </mat-card>
    }
  `,
  styleUrl: './schedule-page.scss',
  styles: `.single { margin-top: 16px; }`,
})
export class DoctorSchedulePage {
  private readonly api = inject(SchedulerApi);

  protected readonly doctorId = inject(AuthService).user()?.doctorId ?? null;
  protected readonly date = signal(new Date());
  protected readonly appointments = signal<Appointment[]>([]);
  protected readonly loading = signal(false);

  constructor() {
    this.goToday();
  }

  /** The clinic's today, not the browser's (ADR-0003); if the clinic call fails, load the browser's date. */
  protected goToday(): void {
    this.api.clinic().subscribe({
      next: (clinic) => this.selectDate(fromIsoDate(clinic.today)),
      error: () => this.load(),
    });
  }

  protected selectDate(date: Date | null): void {
    if (!date) return;
    this.date.set(date);
    this.load();
  }

  protected shiftDay(days: number): void {
    const d = new Date(this.date());
    d.setDate(d.getDate() + days);
    this.selectDate(d);
  }

  private load(): void {
    if (!this.doctorId) return;
    this.loading.set(true);
    this.api.daySchedule(this.doctorId, toIsoDate(this.date())).subscribe({
      next: (list) => {
        this.appointments.set(list);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
