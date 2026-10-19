import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideNativeDateAdapter } from '@angular/material/core';
import { provideRouter } from '@angular/router';
import { Appointment } from '../../core/api/models';
import { toIsoDate } from '../../core/api/scheduler-api';
import { AuthService } from '../../core/auth/auth.service';
import { DoctorSchedulePage } from './doctor-schedule-page';

const appointment: Appointment = {
  id: 'a1', doctorId: 4, doctorName: 'Dr. Test', patientId: 9, patientName: 'Pat Fictional', patientMrn: 'MRN-9',
  startUtc: '2099-11-02T09:00:00Z', endUtc: '2099-11-02T09:30:00Z', localStart: '09:00', localEnd: '09:30',
  reason: 'Check-up', status: 'Scheduled', cancellationReason: null, version: 1,
};

describe('DoctorSchedulePage (AC-004-17, BR-7)', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), provideNativeDateAdapter()],
    });
    http = TestBed.inject(HttpTestingController);
    TestBed.inject(AuthService).login('doc@clinic.test', 'Secret123456').subscribe();
    http.expectOne('/api/v1/auth/login').flush({
      accessToken: 'tok-1', expiresAt: '2099-11-02T10:15:00Z',
      user: { id: 'u3', email: 'doc@clinic.test', roles: ['Doctor'], doctorId: 4 },
    });
  });

  afterEach(() => http.verify());

  const forbidden = (url: string) =>
    url.startsWith('/api/v1/doctors') || url.startsWith('/api/v1/patients') || /\/appointments\/.+\/(cancel|reschedule)$/.test(url);

  it('loads only the own doctor day schedule and renders no booking controls', async () => {
    const fixture = TestBed.createComponent(DoctorSchedulePage);
    await fixture.whenStable();

    const req = http.expectOne((r) => r.url === '/api/v1/appointments');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('doctorId')).toBe('4');
    expect(req.request.params.get('date')).toBe(toIsoDate(new Date()));
    req.flush([appointment]);
    await fixture.whenStable();

    http.expectNone((r) => forbidden(r.url) || r.method !== 'GET');
    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('Pat Fictional');
    expect(el.querySelector('mat-select, app-slot-grid')).toBeNull();
    const buttons = Array.from(el.querySelectorAll('button')).map((b) => b.textContent?.trim() ?? '');
    expect(buttons.some((t) => /book|cancel|reschedule/i.test(t))).toBe(false);
  });

  it('next day reloads the schedule for that date only', async () => {
    const fixture = TestBed.createComponent(DoctorSchedulePage);
    await fixture.whenStable();
    http.expectOne((r) => r.url === '/api/v1/appointments').flush([]);
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('No appointments.');

    fixture.nativeElement.querySelector('button[aria-label="Next day"]').click();
    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);

    const req = http.expectOne((r) => r.url === '/api/v1/appointments');
    expect(req.request.params.get('date')).toBe(toIsoDate(tomorrow));
    req.flush([]);
    http.expectNone((r) => forbidden(r.url) || r.method !== 'GET');
  });
});
