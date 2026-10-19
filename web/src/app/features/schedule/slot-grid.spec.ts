import { TestBed } from '@angular/core/testing';
import { Slot } from '../../core/api/models';
import { SlotGrid } from './slot-grid';

const slot = (localStart: string, status: Slot['status']): Slot => ({
  startUtc: `2026-11-02T${localStart}:00Z`,
  endUtc: `2026-11-02T${localStart}:30Z`,
  localStart,
  status,
  appointmentId: status === 'Booked' ? 'a1' : null,
});

describe('SlotGrid (spec 001)', () => {
  function render(slots: Slot[]) {
    const fixture = TestBed.createComponent(SlotGrid);
    fixture.componentRef.setInput('slots', slots);
    fixture.detectChanges();
    return fixture;
  }

  it('renders one button per slot with clinic-local time and status label (AC-001-6)', () => {
    const fixture = render([slot('09:00', 'Free'), slot('09:30', 'Booked'), slot('10:00', 'PatientBusy')]);
    const buttons: HTMLButtonElement[] = Array.from(fixture.nativeElement.querySelectorAll('button'));

    expect(buttons.map((b) => b.getAttribute('aria-label'))).toEqual(['09:00 Free', '09:30 Booked', '10:00 Patient busy']);
  });

  it('only Free slots are clickable (AC-001-2, AC-001-4)', () => {
    const fixture = render([slot('09:00', 'Past'), slot('09:30', 'Free'), slot('10:00', 'Booked')]);
    const buttons: HTMLButtonElement[] = Array.from(fixture.nativeElement.querySelectorAll('button'));

    expect(buttons.map((b) => b.disabled)).toEqual([true, false, true]);
  });

  it('emits the picked slot', () => {
    const fixture = render([slot('09:30', 'Free')]);
    let picked: Slot | undefined;
    fixture.componentInstance.pick.subscribe((s) => (picked = s));

    fixture.nativeElement.querySelector('button').click();

    expect(picked?.localStart).toBe('09:30');
  });

  it('shows an empty state', () => {
    expect(render([]).nativeElement.textContent).toContain('No slots for this day.');
  });
});
