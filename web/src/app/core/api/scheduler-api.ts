import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  Appointment,
  Availability,
  BookAppointmentRequest,
  Doctor,
  PagedResult,
  Patient,
} from './models';

/** Typed client for /api/v1. Dates are clinic-local `yyyy-MM-dd` strings; the server owns all time-zone logic. */
@Injectable({ providedIn: 'root' })
export class SchedulerApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1';

  doctors(): Observable<Doctor[]> {
    return this.http.get<Doctor[]>(`${this.base}/doctors`);
  }

  searchPatients(search: string, pageSize = 10): Observable<PagedResult<Patient>> {
    const params = new HttpParams().set('search', search).set('pageSize', pageSize);
    return this.http.get<PagedResult<Patient>>(`${this.base}/patients`, { params });
  }

  availability(doctorId: number, date: string, patientId?: number): Observable<Availability> {
    let params = new HttpParams().set('date', date);
    if (patientId) params = params.set('patientId', patientId);
    return this.http.get<Availability>(`${this.base}/doctors/${doctorId}/availability`, { params });
  }

  daySchedule(doctorId: number, date: string): Observable<Appointment[]> {
    const params = new HttpParams().set('doctorId', doctorId).set('date', date);
    return this.http.get<Appointment[]>(`${this.base}/appointments`, { params });
  }

  book(request: BookAppointmentRequest): Observable<Appointment> {
    return this.http.post<Appointment>(`${this.base}/appointments`, request);
  }

  cancel(id: string, reason: string | null): Observable<Appointment> {
    return this.http.post<Appointment>(`${this.base}/appointments/${id}/cancel`, { reason });
  }

  reschedule(id: string, startUtc: string): Observable<Appointment> {
    return this.http.post<Appointment>(`${this.base}/appointments/${id}/reschedule`, { startUtc });
  }
}

/** Formats a datepicker Date as the calendar date the user picked (no UTC shift). */
export function toIsoDate(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}
