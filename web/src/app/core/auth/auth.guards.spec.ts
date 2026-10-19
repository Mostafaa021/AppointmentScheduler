import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideNativeDateAdapter } from '@angular/material/core';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../../app.routes';
import { LoginPage } from '../../features/auth/login-page';
import { DoctorSchedulePage } from '../../features/schedule/doctor-schedule-page';
import { SchedulePage } from '../../features/schedule/schedule-page';
import { Role } from '../api/models';
import { AuthService } from './auth.service';

describe('auth guards and routes (spec 004)', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter(routes), provideNativeDateAdapter()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  function signIn(roles: Role[], doctorId: number | null = null) {
    TestBed.inject(AuthService).login('user@clinic.test', 'Secret123456').subscribe();
    http.expectOne('/api/v1/auth/login').flush({
      accessToken: 'tok-1',
      expiresAt: '2026-11-02T10:15:00Z',
      user: { id: 'u1', email: 'user@clinic.test', roles, doctorId },
    });
  }

  it('AC-004-16: an unauthenticated visitor is redirected to /login', async () => {
    const harness = await RouterTestingHarness.create();
    const page = await harness.navigateByUrl('/schedule');

    expect(TestBed.inject(Router).url).toBe('/login');
    expect(page).toBeInstanceOf(LoginPage);
  });

  it('AC-004-17: a Doctor-only user gets the read-only doctor view', async () => {
    signIn(['Doctor'], 4);
    const harness = await RouterTestingHarness.create();

    expect(await harness.navigateByUrl('/schedule')).toBeInstanceOf(DoctorSchedulePage);
    http.expectNone('/api/v1/doctors');
  });

  it('AC-004-17: Admin/Receptionist (incl. Doctor + Receptionist) get the existing schedule page', async () => {
    signIn(['Doctor', 'Receptionist'], 4);
    const harness = await RouterTestingHarness.create();

    expect(await harness.navigateByUrl('/schedule')).toBeInstanceOf(SchedulePage);
    http.expectOne('/api/v1/doctors').flush([]);
  });
});
