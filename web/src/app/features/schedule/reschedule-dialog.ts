import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Appointment, Slot } from '../../core/api/models';
import { SchedulerApi, toIsoDate } from '../../core/api/scheduler-api';
import { SlotGrid } from './slot-grid';

export interface RescheduleDialogData {
  appointment: Appointment;
  date: Date;
}

/** Spec 003 AC-003-4: pick a new free slot (same doctor). Slots where the patient is busy are shown as PatientBusy. */
@Component({
  selector: 'app-reschedule-dialog',
  imports: [FormsModule, MatDialogModule, MatButtonModule, MatDatepickerModule, MatFormFieldModule, MatInputModule, MatProgressBarModule, SlotGrid],
  template: `
    <h2 mat-dialog-title>Reschedule {{ appt.patientName }}</h2>
    <mat-dialog-content>
      <p>Currently {{ appt.localStart }} with {{ appt.doctorName }}.</p>
      <mat-form-field appearance="outline">
        <mat-label>New date</mat-label>
        <input matInput [matDatepicker]="picker" [ngModel]="date()" (ngModelChange)="setDate($event)" />
        <mat-datepicker-toggle matIconSuffix [for]="picker" />
        <mat-datepicker #picker />
      </mat-form-field>
      @if (loading()) { <mat-progress-bar mode="indeterminate" /> }
      <app-slot-grid [slots]="slots()" [selected]="selected()?.startUtc ?? null" (pick)="selected.set($event)" />
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Close</button>
      <button mat-flat-button [disabled]="!selected() || saving()" (click)="confirm()">
        Move to {{ selected()?.localStart ?? '…' }}
      </button>
    </mat-dialog-actions>
  `,
})
export class RescheduleDialog {
  private readonly data = inject<RescheduleDialogData>(MAT_DIALOG_DATA);
  private readonly api = inject(SchedulerApi);
  private readonly ref = inject(MatDialogRef<RescheduleDialog, boolean>);

  protected readonly appt = this.data.appointment;
  protected readonly date = signal(this.data.date);
  protected readonly slots = signal<Slot[]>([]);
  protected readonly selected = signal<Slot | null>(null);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);

  constructor() {
    this.load();
  }

  protected setDate(date: Date | null): void {
    if (!date) return;
    this.date.set(date);
    this.load();
  }

  private load(): void {
    this.selected.set(null);
    this.loading.set(true);
    this.api.availability(this.appt.doctorId, toIsoDate(this.date()), this.appt.patientId).subscribe({
      next: (a) => {
        this.slots.set(a.slots);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected confirm(): void {
    const slot = this.selected();
    if (!slot) return;
    this.saving.set(true);
    this.api.reschedule(this.appt.id, slot.startUtc).subscribe({
      next: () => this.ref.close(true),
      error: () => {
        this.saving.set(false);
        this.load(); // conflict or rule violation: show fresh availability
      },
    });
  }
}
