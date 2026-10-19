# 005 — Patients

| Status | Refined ☑ · Designed ☐ · Built ☐ · Tested ☐ · Reviewed ☐ · Released ☐ · Documented ☐ |
|---|---|
| Milestone | M1 (search only) → M4 (CRUD) |
| Business rules touched | BR-6 |

## Goal
Receptionists find patients quickly when booking and maintain patient records.

## Acceptance criteria
- **AC-005-1** `GET /patients?search=&page=&pageSize=` matches name (case-insensitive, contains) or exact MRN; paged, max 100,
  returns `{items, page, pageSize, total}`; inactive patients excluded unless `includeInactive=true`.
- **AC-005-2** `POST /patients` creates a patient; MRN generated server-side (`MRN-000123`); full name 2–200 chars, DOB in the past,
  phone/email validated → 201.
- **AC-005-3** `PUT /patients/{id}` updates contact details → 200; unknown → 404.
- **AC-005-4** (BR-6) `POST /patients/{id}/deactivate` with future Scheduled appointments → 422 `patient_has_future_appointments`;
  otherwise 200 and the patient can no longer be booked (422 `patient_inactive`).

## Data changes
`patients(id, mrn unique, full_name, date_of_birth, phone, email, is_active)`; trigram index on `full_name` for search (M4).

## NFR notes
Roles Admin, Receptionist. PII: never logged; responses only to authorized roles.

## Out of scope
Medical records, insurance, merging duplicates.
