import { HttpErrorResponse } from '@angular/common/http';
import { errorMessage } from './api-error.interceptor';

describe('errorMessage', () => {
  it('prefers field validation errors', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { success: false, statusCode: 400, errorCode: 'VALIDATION_FAILED', message: 'One or more…', errors: { reason: ['Reason is required.'] } },
    });
    expect(errorMessage(error)).toBe('Reason is required.');
  });

  it('uses the ApiResponse message for business errors', () => {
    const error = new HttpErrorResponse({
      status: 409,
      error: { success: false, statusCode: 409, errorCode: 'DOCTOR_CONFLICT', message: 'The doctor already has an appointment at this time.' },
    });
    expect(errorMessage(error)).toBe('The doctor already has an appointment at this time.');
  });

  it('falls back to the status when the body is not an ApiResponse', () => {
    expect(errorMessage(new HttpErrorResponse({ status: 502, error: '<html>Bad gateway</html>' }))).toBe('Request failed (502).');
  });

  it('explains network failures', () => {
    expect(errorMessage(new HttpErrorResponse({ status: 0 }))).toContain('Cannot reach the server');
  });
});
