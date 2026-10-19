import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, provideRouter } from '@angular/router';
import { authInterceptor } from '../../core/auth/auth.interceptor';
import { apiErrorInterceptor } from '../../core/http/api-error.interceptor';
import { apiResponseInterceptor } from '../../core/http/api-response.interceptor';
import { LoginPage } from './login-page';

describe('LoginPage (AC-004-15)', () => {
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
    http = TestBed.inject(HttpTestingController);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    snack = vi.spyOn(TestBed.inject(MatSnackBar), 'open').mockReturnValue(undefined as never);
  });

  afterEach(() => {
    http.verify();
    vi.restoreAllMocks();
  });

  async function render() {
    const fixture = TestBed.createComponent(LoginPage);
    await fixture.whenStable();
    const el: HTMLElement = fixture.nativeElement;
    const type = (selector: string, value: string) => {
      const input = el.querySelector<HTMLInputElement>(selector)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    type('input[name=email]', 'reception@clinic.test');
    type('input[name=password]', 'Secret123456');
    const button = el.querySelector<HTMLButtonElement>('button[type=submit]')!;
    return { fixture, el, button };
  }

  it('shows labelled e-mail and password fields', async () => {
    const { el } = await render();
    const labels = Array.from(el.querySelectorAll('mat-label')).map((l) => l.textContent?.trim());

    expect(labels).toEqual(['E-mail', 'Password']);
    expect(el.querySelector('input[name=email]')?.getAttribute('autocomplete')).toBe('username');
    expect(el.querySelector('input[name=password]')?.getAttribute('type')).toBe('password');
  });

  it('disables submit while pending and navigates to /schedule on 200', async () => {
    const { fixture, button } = await render();
    button.click();
    await fixture.whenStable();

    expect(button.disabled).toBe(true);
    const req = http.expectOne('/api/v1/auth/login');
    expect(req.request.body).toEqual({ email: 'reception@clinic.test', password: 'Secret123456' });
    req.flush({
      success: true,
      statusCode: 200,
      data: { accessToken: 't', expiresAt: '2026-11-02T10:15:00Z', user: { id: 'u', email: 'e', roles: ['Receptionist'] } },
    });

    expect(navigate).toHaveBeenCalledWith('/schedule');
  });

  for (const [status, errorCode, message] of [
    [401, 'INVALID_CREDENTIALS', 'Invalid e-mail or password.'],
    [423, 'ACCOUNT_LOCKED', 'The account is locked. Try again later.'],
  ] as const) {
    it(`stays on /login, re-enables submit and shows the server message on ${status} ${errorCode}`, async () => {
      const { fixture, button } = await render();
      button.click();
      await fixture.whenStable();

      http.expectOne('/api/v1/auth/login').flush({ success: false, statusCode: status, errorCode, message }, { status, statusText: errorCode });
      await fixture.whenStable();

      http.expectNone('/api/v1/auth/refresh');
      expect(navigate).not.toHaveBeenCalled();
      expect(snack).toHaveBeenCalledWith(message, 'Dismiss', expect.anything());
      expect(button.disabled).toBe(false);
    });
  }
});
