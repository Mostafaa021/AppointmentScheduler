import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, provideRouter } from '@angular/router';
import { ApiResponse, AuthResponse } from '../api/models';
import { apiErrorInterceptor } from '../http/api-error.interceptor';
import { apiResponseInterceptor } from '../http/api-response.interceptor';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

// Real server shape: the envelope, unwrapped by apiResponseInterceptor before AuthService sees it (ADR-0015).
const response = (token: string): ApiResponse<AuthResponse> => ({
  success: true,
  statusCode: 200,
  data: {
    accessToken: token,
    expiresAt: '2026-11-02T10:15:00Z',
    user: { id: 'u1', email: 'user@clinic.test', roles: ['Receptionist'] },
  },
});
const unauthorized = { status: 401, statusText: 'Unauthorized' };

describe('authInterceptor (spec 004)', () => {
  let client: HttpClient;
  let auth: AuthService;
  let http: HttpTestingController;
  let navigate: ReturnType<typeof vi.spyOn>;
  let snack: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([apiErrorInterceptor, authInterceptor, apiResponseInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    client = TestBed.inject(HttpClient);
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    snack = vi.spyOn(TestBed.inject(MatSnackBar), 'open').mockReturnValue(undefined as never);

    auth.login('user@clinic.test', 'Secret123456').subscribe();
    http.expectOne('/api/v1/auth/login').flush(response('tok-1'));
  });

  afterEach(() => {
    http.verify();
    vi.restoreAllMocks();
  });

  it('AC-004-18: adds Bearer to /api/v1 routes only, never to login/refresh or another origin', () => {
    client.get('/api/v1/appointments').subscribe();
    client.post('/api/v1/auth/login', {}).subscribe();
    client.post('/api/v1/auth/refresh', null).subscribe();
    client.get('https://other.example/api/v1/appointments').subscribe();
    client.get('//other.example/api/v1/appointments').subscribe();
    client.get('/assets/x.json').subscribe();

    expect(http.expectOne('/api/v1/appointments').request.headers.get('Authorization')).toBe('Bearer tok-1');
    for (const url of ['/api/v1/auth/login', '/api/v1/auth/refresh', 'https://other.example/api/v1/appointments',
      '//other.example/api/v1/appointments', '/assets/x.json']) {
      const req = http.expectOne(url);
      expect(req.request.headers.has('Authorization')).toBe(false);
      req.flush({});
    }
  });

  it('AC-004-19: N concurrent 401s → exactly one refresh, all retried once with the new token, no error shown', () => {
    const results: unknown[] = [];
    for (const id of ['a', 'b', 'c']) client.get(`/api/v1/appointments/${id}`).subscribe((r) => results.push(r));

    for (const id of ['a', 'b', 'c']) http.expectOne(`/api/v1/appointments/${id}`).flush({}, unauthorized);
    http.expectOne('/api/v1/auth/refresh').flush(response('tok-2'));

    for (const id of ['a', 'b', 'c']) {
      const retry = http.expectOne(`/api/v1/appointments/${id}`);
      expect(retry.request.headers.get('Authorization')).toBe('Bearer tok-2');
      retry.flush({ id });
    }
    expect(results).toEqual([{ id: 'a' }, { id: 'b' }, { id: 'c' }]);
    expect(snack).not.toHaveBeenCalled();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('AC-004-19: a late 401 for a request sent with the old token retries with the new one without refreshing', () => {
    client.get('/api/v1/appointments/a').subscribe();
    client.get('/api/v1/appointments/b').subscribe();
    const [a, b] = [http.expectOne('/api/v1/appointments/a'), http.expectOne('/api/v1/appointments/b')];

    a.flush({}, unauthorized);
    http.expectOne('/api/v1/auth/refresh').flush(response('tok-2'));
    http.expectOne('/api/v1/appointments/a').flush({});
    b.flush({}, unauthorized);

    expect(http.expectOne('/api/v1/appointments/b').request.headers.get('Authorization')).toBe('Bearer tok-2');
    http.expectNone('/api/v1/auth/refresh');
  });

  it('AC-004-19/20: a retried request that returns 401 again is not refreshed again → logout to /login', () => {
    let failed = false;
    client.get('/api/v1/appointments/a').subscribe({ error: () => (failed = true) });

    http.expectOne('/api/v1/appointments/a').flush({}, unauthorized);
    http.expectOne('/api/v1/auth/refresh').flush(response('tok-2'));
    http.expectOne('/api/v1/appointments/a').flush({}, unauthorized);

    http.expectNone('/api/v1/auth/refresh');
    expect(failed).toBe(true);
    expect(auth.accessToken()).toBeNull();
    expect(navigate).toHaveBeenCalledWith('/login');
  });

  it('AC-004-20: refresh failure clears the session and goes to /login', () => {
    let failed = false;
    client.get('/api/v1/appointments/a').subscribe({ error: () => (failed = true) });

    http.expectOne('/api/v1/appointments/a').flush({}, unauthorized);
    http.expectOne('/api/v1/auth/refresh').flush({ success: false, statusCode: 401, errorCode: 'INVALID_REFRESH_TOKEN', message: 'Sign in again.' }, unauthorized);

    expect(failed).toBe(true);
    expect(auth.user()).toBeNull();
    expect(navigate).toHaveBeenCalledWith('/login');
  });

  it('non-401 errors pass through to the error snackbar without refresh', () => {
    client.get('/api/v1/appointments/a').subscribe({ error: () => undefined });
    http.expectOne('/api/v1/appointments/a').flush({ success: false, statusCode: 403, errorCode: 'FORBIDDEN', message: 'Forbidden.' }, { status: 403, statusText: 'Forbidden' });

    http.expectNone('/api/v1/auth/refresh');
    expect(snack).toHaveBeenCalledWith('Forbidden.', 'Dismiss', expect.anything());
  });
});
