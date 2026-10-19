import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { forkJoin } from 'rxjs';
import { Appointment, Slot } from '../../core/api/models';
import { SchedulerApi, toIsoDate } from '../../core/api/scheduler-api';
import { BookDialog, BookDialogData } from './book-dialog';
import { CancelDialog } from './cancel-dialog';
import { RescheduleDialog, RescheduleDialogData } from './reschedule-dialog';
import { SlotGrid } from './slot-grid';

/** Specs 001–003: pick doctor + date, see slots, book, and manage the day's appointments. */
@Component({
  selector: 'app-schedule-page',
  imports: [
    FormsModule, MatButtonModule, MatCardModule, MatChipsModule, MatDatepickerModule, MatFormFieldModule, MatIconModule,
    MatInputModule, MatProgressBarModule, MatSelectModule, MatTooltipModule, SlotGrid,
  ],
  templateUrl: './schedule-page.html',
  styleUrl: './schedule-page.scss',
})
export class SchedulePage {
  private readonly api = inject(SchedulerApi);
  private readonly dialog = inject(MatDialog);

  protected readonly doctors = toSignal(this.api.doctors(), { initialValue: [] });
  protected readonly doctorId = signal<number | null>(null);
  protected readonly date = signal(new Date());
  protected readonly doctor = computed(() => this.doctors().find((d) => d.id === this.doctorId()) ?? null);

  protected readonly slots = signal<Slot[]>([]);
  protected readonly appointments = signal<Appointment[]>([]);
  protected readonly timeZone = signal('');
  protected readonly loading = signal(false);
  protected readonly freeCount = computed(() => this.slots().filter((s) => s.status === 'Free').length);
  protected readonly today = () => new Date();

  protected selectDoctor(id: number): void {
    this.doctorId.set(id);
    this.load();
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

  protected load(): void {
    const doctorId = this.doctorId();
    if (!doctorId) return;
    const date = toIsoDate(this.date());
    this.loading.set(true);
    forkJoin({ availability: this.api.availability(doctorId, date), schedule: this.api.daySchedule(doctorId, date) }).subscribe({
      next: ({ availability, schedule }) => {
        this.slots.set(availability.slots);
        this.timeZone.set(availability.timeZone);
        this.appointments.set(schedule);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected book(slot: Slot): void {
    const doctor = this.doctor();
    if (!doctor) return;
    const data: BookDialogData = { doctor, date: toIsoDate(this.date()), timeZone: this.timeZone(), slot };
    this.dialog.open(BookDialog, { data, width: '520px' }).afterClosed().subscribe((result) => result && this.load());
  }

  protected cancel(appointment: Appointment): void {
    this.dialog.open(CancelDialog, { data: appointment, width: '440px' }).afterClosed().subscribe((changed) => changed && this.load());
  }

  protected reschedule(appointment: Appointment): void {
    const data: RescheduleDialogData = { appointment, date: this.date() };
    this.dialog.open(RescheduleDialog, { data, width: '640px' }).afterClosed().subscribe((changed) => changed && this.load());
  }

  protected isModifiable(a: Appointment): boolean {
    return a.status === 'Scheduled' && new Date(a.startUtc).getTime() > Date.now();
  }
}
