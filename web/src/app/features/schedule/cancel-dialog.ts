import { Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Appointment } from '../../core/api/models';
import { SchedulerApi } from '../../core/api/scheduler-api';

/** Spec 003 AC-003-2: confirm and cancel with an optional reason. */
@Component({
  selector: 'app-cancel-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Cancel appointment?</h2>
    <mat-dialog-content>
      <p>{{ appointment.patientName }} · {{ appointment.localStart }}–{{ appointment.localEnd }} with {{ appointment.doctorName }}</p>
      <mat-form-field appearance="outline" class="full">
        <mat-label>Reason (optional)</mat-label>
        <input matInput [formControl]="reason" maxlength="500" />
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Keep</button>
      <button mat-flat-button class="danger" (click)="confirm()" [disabled]="saving()">Cancel appointment</button>
    </mat-dialog-actions>
  `,
  styles: `.full { width: 100%; } .danger { background: var(--mat-sys-error); color: var(--mat-sys-on-error); }`,
})
export class CancelDialog {
  protected readonly appointment = inject<Appointment>(MAT_DIALOG_DATA);
  private readonly api = inject(SchedulerApi);
  private readonly ref = inject(MatDialogRef<CancelDialog, boolean>);
  protected readonly reason = new FormControl('', Validators.maxLength(500));
  protected readonly saving = signal(false);

  protected confirm(): void {
    this.saving.set(true);
    this.api.cancel(this.appointment.id, this.reason.value?.trim() || null).subscribe({
      next: () => this.ref.close(true),
      error: () => this.ref.close(true), // state changed elsewhere (409/422): refresh either way
    });
  }
}
