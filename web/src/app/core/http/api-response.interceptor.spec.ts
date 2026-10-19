import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { apiResponseInterceptor } from './api-response.interceptor';

describe('apiResponseInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([apiResponseInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('returns the envelope data to the caller', async () => {
    const result = firstValueFrom(http.get('/api/v1/doctors'));
    backend.expectOne('/api/v1/doctors').flush({ success: true, statusCode: 200, data: [{ id: 1 }] });
    expect(await result).toEqual([{ id: 1 }]);
  });

  it('leaves non-API responses untouched', async () => {
    const result = firstValueFrom(http.get('/assets/config.json'));
    backend.expectOne('/assets/config.json').flush({ success: true, statusCode: 200, data: 'x' });
    expect(await result).toEqual({ success: true, statusCode: 200, data: 'x' });
  });

  it('passes errors through with the envelope as error body', async () => {
    const result = firstValueFrom(http.get('/api/v1/doctors')).catch((e) => e);
    backend
      .expectOne('/api/v1/doctors')
      .flush({ success: false, statusCode: 403, errorCode: 'FORBIDDEN', message: 'No.' }, { status: 403, statusText: 'Forbidden' });
    expect((await result).error.errorCode).toBe('FORBIDDEN');
  });
});
