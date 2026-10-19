import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SchedulerApi, toIsoDate } from './scheduler-api';

describe('SchedulerApi', () => {
  let api: SchedulerApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(SchedulerApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('requests availability with date and optional patient', () => {
    api.availability(3, '2026-11-02', 7).subscribe();

    const req = http.expectOne((r) => r.url === '/api/v1/doctors/3/availability');
    expect(req.request.params.get('date')).toBe('2026-11-02');
    expect(req.request.params.get('patientId')).toBe('7');
    req.flush({ doctorId: 3, date: '2026-11-02', timeZone: 'Europe/London', slots: [] });
  });

  it('posts bookings with the UTC start from the slot', () => {
    api.book({ doctorId: 1, patientId: 2, startUtc: '2026-11-02T10:00:00+00:00', reason: 'Check-up' }).subscribe();

    const req = http.expectOne('/api/v1/appointments');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.startUtc).toBe('2026-11-02T10:00:00+00:00');
    req.flush({});
  });

  it('posts reschedule and cancel to their action routes', () => {
    api.reschedule('abc', '2026-11-03T09:00:00Z').subscribe();
    api.cancel('abc', null).subscribe();

    expect(http.expectOne('/api/v1/appointments/abc/reschedule').request.body).toEqual({ startUtc: '2026-11-03T09:00:00Z' });
    expect(http.expectOne('/api/v1/appointments/abc/cancel').request.body).toEqual({ reason: null });
  });

  it('toIsoDate keeps the picked calendar day regardless of time zone', () => {
    expect(toIsoDate(new Date(2026, 10, 2, 23, 59))).toBe('2026-11-02');
    expect(toIsoDate(new Date(2026, 0, 5, 0, 0))).toBe('2026-01-05');
  });
});
