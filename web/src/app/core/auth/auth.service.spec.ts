import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthResponse, Role } from '../api/models';
import { SILENT } from '../http/api-error.interceptor';
import { AuthService } from './auth.service';

const response = (token: string, roles: Role[] = ['Receptionist'], doctorId: number | null = null): AuthResponse => ({
  accessToken: token,
  expiresAt: '2026-11-02T10:15:00Z',
  user: { id: 'u1', email: 'user@clinic.test', roles, doctorId },
});

describe('AuthService (spec 004)', () => {
  let auth: AuthService;
  let http: HttpTestingController;
  let navigate: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  });

  afterEach(() => {
    http.verify();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('login keeps token and user in memory (AC-004-1, AC-004-15)', () => {
    auth.login('user@clinic.test', 'Secret123456').subscribe();

    const req = http.expectOne('/api/v1/auth/login');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: 'user@clinic.test', password: 'Secret123456' });
    req.flush(response('tok-1'));

    expect(auth.accessToken()).toBe('tok-1');
    expect(auth.user()?.email).toBe('user@clinic.test');
    expect(auth.isAuthenticated()).toBe(true);
  });

  it('AC-004-21: login and refresh never write the token to localStorage, sessionStorage, IndexedDB or document.cookie', () => {
    const setItem = vi.spyOn(Storage.prototype, 'setItem');
    const idbOpen = vi.fn();
    vi.stubGlobal('indexedDB', { open: idbOpen });
    const cookieSet = vi.fn();
    Object.defineProperty(document, 'cookie', { configurable: true, get: () => '', set: cookieSet });
    try {
      auth.login('user@clinic.test', 'Secret123456').subscribe();
      http.expectOne('/api/v1/auth/login').flush(response('tok-1'));
      auth.refresh().subscribe();
      http.expectOne('/api/v1/auth/refresh').flush(response('tok-2'));

      expect(auth.accessToken()).toBe('tok-2');
      expect(setItem).not.toHaveBeenCalled();
      expect(idbOpen).not.toHaveBeenCalled();
      expect(cookieSet).not.toHaveBeenCalled();
    } finally {
      delete (document as unknown as Record<string, unknown>)['cookie'];
    }
  });

  it('refresh is single-flight, sends the cookie and is silent (AC-004-5, AC-004-19)', () => {
    const tokens: string[] = [];
    auth.refresh().subscribe((t) => tokens.push(t));
    auth.refresh().subscribe((t) => tokens.push(t));

    const req = http.expectOne('/api/v1/auth/refresh');
    expect(req.request.withCredentials).toBe(true);
    expect(req.request.context.get(SILENT)).toBe(true);
    req.flush(response('tok-2', ['Doctor'], 4));

    expect(tokens).toEqual(['tok-2', 'tok-2']);
    expect(auth.isDoctorOnly()).toBe(true);
    auth.refresh().subscribe();
    http.expectOne('/api/v1/auth/refresh').flush(response('tok-3'));
  });

  it('restore resolves when refresh fails and leaves the user signed out (AC-004-16)', async () => {
    const done = auth.restore();
    http.expectOne('/api/v1/auth/refresh').flush({ success: false, statusCode: 401, errorCode: 'INVALID_REFRESH_TOKEN', message: 'Sign in again.' }, { status: 401, statusText: 'Unauthorized' });

    await expect(done).resolves.toBeUndefined();
    expect(auth.isAuthenticated()).toBe(false);
  });

  it('logout clears memory and goes to /login even when the call fails (AC-004-20)', () => {
    auth.login('user@clinic.test', 'Secret123456').subscribe();
    http.expectOne('/api/v1/auth/login').flush(response('tok-1'));

    auth.logout();
    const req = http.expectOne('/api/v1/auth/logout');
    expect(req.request.withCredentials).toBe(true);
    req.flush(null, { status: 500, statusText: 'Server Error' });

    expect(auth.accessToken()).toBeNull();
    expect(auth.user()).toBeNull();
    expect(navigate).toHaveBeenCalledWith('/login');
  });

  it('isDoctorOnly is false when Doctor is combined with another role (BR-7 union)', () => {
    auth.login('x@clinic.test', 'Secret123456').subscribe();
    http.expectOne('/api/v1/auth/login').flush(response('tok-1', ['Doctor', 'Receptionist'], 4));

    expect(auth.isDoctorOnly()).toBe(false);
  });
});
