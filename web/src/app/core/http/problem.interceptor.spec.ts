import { HttpErrorResponse } from '@angular/common/http';
import { problemMessage } from './problem.interceptor';

describe('problemMessage', () => {
  it('prefers field validation errors', () => {
    const error = new HttpErrorResponse({ status: 400, error: { detail: 'One or more…', errors: { reason: ['Reason is required.'] } } });
    expect(problemMessage(error)).toBe('Reason is required.');
  });

  it('uses the ProblemDetails detail for business errors', () => {
    const error = new HttpErrorResponse({ status: 409, error: { code: 'doctor_conflict', detail: 'The doctor already has an appointment at this time.' } });
    expect(problemMessage(error)).toBe('The doctor already has an appointment at this time.');
  });

  it('explains network failures', () => {
    expect(problemMessage(new HttpErrorResponse({ status: 0 }))).toContain('Cannot reach the server');
  });
});
