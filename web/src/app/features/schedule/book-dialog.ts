import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { debounceTime, distinctUntilChanged, filter, map, of, startWith, switchMap } from 'rxjs';
import { Doctor, Patient, Slot } from '../../core/api/models';
import { SchedulerApi } from '../../core/api/scheduler-api';

export interface BookDialogData {
  doctor: Doctor;
  date: string;
  timeZone: string;
  slot: Slot;
}

/** Spec 002: choose patient + reason, review, confirm. Closes with 'booked' or 'refresh' (slot taken meanwhile). */
@Component({
  selector: 'app-book-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatAutocompleteModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Book appointment</h2>
    <mat-dialog-content>
      <p class="summary">
        <strong>{{ data.doctor.fullName }}</strong> · {{ data.date }} at <strong>{{ data.slot.localStart }}</strong>
        <span class="tz">({{ data.timeZone }})</span>
      </p>

      @if (!confirming()) {
        <mat-form-field appearance="outline" class="full">
          <mat-label>Patient (name or MRN)</mat-label>
          <input matInput [formControl]="patientSearch" [matAutocomplete]="auto" required />
          <mat-autocomplete #auto="matAutocomplete" [displayWith]="displayPatient" (optionSelected)="patient.set($event.option.value)">
            @for (p of patients(); track p.id) {
              <mat-option [value]="p">{{ p.fullName }} · {{ p.mrn }}</mat-option>
            }
          </mat-autocomplete>
          <mat-hint>Start typing to search</mat-hint>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Reason for visit</mat-label>
          <textarea matInput [formControl]="reason" rows="3" maxlength="500" required></textarea>
          <mat-hint align="end">{{ reason.value?.length ?? 0 }}/500</mat-hint>
        </mat-form-field>
      } @else {
        <dl class="review">
          <dt>Patient</dt><dd>{{ patient()?.fullName }} ({{ patient()?.mrn }})</dd>
          <dt>Reason</dt><dd>{{ reason.value }}</dd>
        </dl>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      @if (!confirming()) {
        <button mat-button mat-dialog-close>Cancel</button>
        <button mat-flat-button [disabled]="!patient() || reason.invalid" (click)="confirming.set(true)">Review</button>
      } @else {
        <button mat-button (click)="confirming.set(false)" [disabled]="saving()">Back</button>
        <button mat-flat-button (click)="confirm()" [disabled]="saving()">Confirm booking</button>
      }
    </mat-dialog-actions>
  `,
  styles: `
    .full { width: 100%; }
    .summary { margin-top: 0; }
    .tz { color: var(--mat-sys-on-surface-variant); }
    .review dt { font-weight: 600; } .review dd { margin: 0 0 12px; }
  `,
})
export class BookDialog {
  protected readonly data = inject<BookDialogData>(MAT_DIALOG_DATA);
  private readonly api = inject(SchedulerApi);
  private readonly ref = inject(MatDialogRef<BookDialog, 'booked' | 'refresh'>);

  protected readonly patientSearch = new FormControl<string | Patient>('', { nonNullable: true });
  protected readonly reason = new FormControl('', [Validators.required, Validators.maxLength(500), Validators.pattern(/\S/)]);
  protected readonly patient = signal<Patient | null>(null);
  protected readonly confirming = signal(false);
  protected readonly saving = signal(false);

  protected readonly patients = toSignal(
    this.patientSearch.valueChanges.pipe(
      startWith(''),
      filter((v): v is string => typeof v === 'string'),
      debounceTime(250),
      distinctUntilChanged(),
      switchMap((term) => (term.trim().length < 2 ? of([]) : this.api.searchPatients(term).pipe(map((r) => r.items)))),
    ),
    { initialValue: [] as Patient[] },
  );

  constructor() {
    // Typing after a selection clears it, so a stale patient can't be booked.
    this.patientSearch.valueChanges
      .pipe(filter((v) => typeof v === 'string'), takeUntilDestroyed())
      .subscribe(() => this.patient.set(null));
  }

  protected displayPatient = (p: Patient | string | null) => (typeof p === 'string' ? p : p ? `${p.fullName} · ${p.mrn}` : '');

  protected confirm(): void {
    const patient = this.patient();
    if (!patient) return;
    this.saving.set(true);
    this.api
      .book({ doctorId: this.data.doctor.id, patientId: patient.id, startUtc: this.data.slot.startUtc, reason: this.reason.value!.trim() })
      .subscribe({
        next: () => this.ref.close('booked'),
        error: (e: HttpErrorResponse) => {
          this.saving.set(false);
          if (e.status === 409 && e.error?.code === 'doctor_conflict') this.ref.close('refresh'); // slot taken meanwhile
          else this.confirming.set(false);
        },
      });
  }
}
