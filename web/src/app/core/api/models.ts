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

/** GET /clinic — `today` is the clinic's local date (`yyyy-MM-dd`), which may differ from the browser's (ADR-0003). */
export interface Clinic {
  timeZone: string;
  today: string;
  opens: string;
  closes: string;
  slotMinutes: number;
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

/**
 * ADR-0015: the envelope of every API body. Success bodies are unwrapped to `data` by apiResponseInterceptor; error bodies
 * reach callers as HttpErrorResponse.error, where `errorCode` is stable (e.g. DOCTOR_CONFLICT).
 */
export interface ApiResponse<T> {
  isSuccess: boolean;
  statusCode: number;
  message?: string;
  data?: T;
  /** Exception type + message for unexpected 500s, Development only. Never show it to end users. */
  errorDetails?: string;
  errorCode?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
  /** ISO 8601 UTC time the server built the response. */
  timestamp: string;
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
