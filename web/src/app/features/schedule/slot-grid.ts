import { Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Slot, SlotStatus } from '../../core/api/models';

const LABELS: Record<SlotStatus, string> = {
  Free: 'Free',
  Booked: 'Booked',
  Past: 'Past',
  PatientBusy: 'Patient busy',
};

/** Presentational grid of a day's slots. Only Free slots are clickable (UI hint only; the API enforces the rules). */
@Component({
  selector: 'app-slot-grid',
  imports: [MatButtonModule, MatTooltipModule],
  template: `
    <div class="grid" role="list" aria-label="Slots">
      @for (slot of slots(); track slot.startUtc) {
        <button
          mat-stroked-button
          role="listitem"
          class="slot"
          [class]="'slot status-' + slot.status"
          [class.selected]="slot.startUtc === selected()"
          [disabled]="slot.status !== 'Free'"
          [attr.aria-label]="slot.localStart + ' ' + label(slot.status)"
          [matTooltip]="label(slot.status)"
          (click)="pick.emit(slot)"
        >
          <span class="time">{{ slot.localStart }}</span>
          <span class="state">{{ label(slot.status) }}</span>
        </button>
      } @empty {
        <p class="empty">No slots for this day.</p>
      }
    </div>
  `,
  styles: `
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(104px, 1fr)); gap: 8px; }
    .slot { height: auto; padding: 8px 4px; display: flex; flex-direction: column; line-height: 1.2; }
    .slot .time { font: var(--mat-sys-title-medium); display: block; }
    .slot .state { font: var(--mat-sys-label-small); display: block; }
    .status-Free { border-color: var(--mat-sys-primary); }
    .status-Booked { background: var(--mat-sys-secondary-container); }
    .status-PatientBusy { background: var(--mat-sys-tertiary-container); }
    .selected { background: var(--mat-sys-primary); color: var(--mat-sys-on-primary); }
    .empty { color: var(--mat-sys-on-surface-variant); }
  `,
})
export class SlotGrid {
  readonly slots = input.required<Slot[]>();
  readonly selected = input<string | null>(null);
  readonly pick = output<Slot>();

  protected label(status: SlotStatus): string {
    return LABELS[status];
  }
}
