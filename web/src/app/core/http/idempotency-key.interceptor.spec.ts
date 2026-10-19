import { HttpClient, HttpHeaders, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { IDEMPOTENCY_KEY_HEADER, idempotencyKeyInterceptor } from './idempotency-key.interceptor';

describe('idempotencyKeyInterceptor (spec 009)', () => {
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([idempotencyKeyInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('adds a UUID key to API writes', () => {
    http.post('/api/v1/appointments', {}).subscribe();
    http.post('/api/v1/appointments/a/cancel', {}).subscribe();

    const [first, second] = backend.match((r) => r.url.startsWith('/api/v1/appointments'));
    const key = first.request.headers.get(IDEMPOTENCY_KEY_HEADER);
    expect(key).toMatch(/^[0-9a-f-]{36}$/);
    expect(second.request.headers.get(IDEMPOTENCY_KEY_HEADER)).not.toBe(key); // one key per action
    first.flush({});
    second.flush({});
  });

  it('keeps a key the caller already set', () => {
    http.post('/api/v1/appointments', {}, { headers: new HttpHeaders({ [IDEMPOTENCY_KEY_HEADER]: 'mine' }) }).subscribe();

    const req = backend.expectOne('/api/v1/appointments');
    expect(req.request.headers.get(IDEMPOTENCY_KEY_HEADER)).toBe('mine');
    req.flush({});
  });

  it('leaves reads, auth calls and non-API URLs alone', () => {
    http.get('/api/v1/doctors').subscribe();
    http.post('/api/v1/auth/login', {}).subscribe();
    http.post('/assets/x', {}).subscribe();

    for (const req of backend.match(() => true)) {
      expect(req.request.headers.has(IDEMPOTENCY_KEY_HEADER)).toBe(false);
      req.flush({});
    }
  });
});
