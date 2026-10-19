// Mirrors the API contract in docs/specs/001-003. Times: *Utc are ISO-8601 instants; local* are clinic wall-clock "HH:mm".

export interface Doctor {
  id: number;
  fullName: string;
  specialty: string;
}

export interface Patient {
  id: number;
  mrn: string;
  fullName: string;
  dateOfBirth: string;
  phone: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

export type SlotStatus = 'Free' | 'Booked' | 'Past' | 'PatientBusy';

export interface Slot {
  startUtc: string;
  endUtc: string;
  localStart: string;
  status: SlotStatus;
  appointmentId: string | null;
}

export interface Availability {
  doctorId: number;
  date: string;
  timeZone: string;
  slots: Slot[];
}

export type AppointmentStatus = 'Scheduled' | 'Cancelled';

export interface Appointment {
  id: string;
  doctorId: number;
  doctorName: string;
  patientId: number;
  patientName: string;
  patientMrn: string;
  startUtc: string;
  endUtc: string;
  localStart: string;
  localEnd: string;
  reason: string;
  status: AppointmentStatus;
  cancellationReason: string | null;
  version: number;
}

export interface BookAppointmentRequest {
  doctorId: number;
  patientId: number;
  startUtc: string;
  reason: string;
}

/** RFC 7807 body returned by the API for every error; `code` is stable (e.g. doctor_conflict). */
export interface Problem {
  status: number;
  title?: string;
  detail?: string;
  code?: string;
  errors?: Record<string, string[]>;
}

export type Role = 'Admin' | 'Receptionist' | 'Doctor';

/** Spec 004: the signed-in user as returned by login/refresh/me. */
export interface AuthUser {
  id: string;
  email: string;
  roles: Role[];
  doctorId?: number | null;
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: AuthUser;
}
