import { HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { map } from 'rxjs';
import { ApiResponse } from '../api/models';

/**
 * ADR-0015: every /api body is an ApiResponse envelope. Unwraps `data` so services and components keep working with the DTO
 * types. Registered innermost, so the auth and error interceptors also see DTOs. Error bodies are left alone
 * (they arrive as HttpErrorResponse.error).
 */
export const apiResponseInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    map((event) =>
      event instanceof HttpResponse && req.url.startsWith('/api/') && isApiResponse(event.body)
        ? event.clone({ body: event.body.data ?? null })
        : event,
    ),
  );

function isApiResponse(body: unknown): body is ApiResponse<unknown> {
  return typeof body === 'object' && body !== null && 'isSuccess' in body && 'statusCode' in body;
}
