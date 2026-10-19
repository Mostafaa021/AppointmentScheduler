import { HttpInterceptorFn } from '@angular/common/http';

/** Spec 009: the header every POST carries; the server replays the first response for a repeated key. */
export const IDEMPOTENCY_KEY_HEADER = 'Idempotency-Key';

/**
 * Adds a fresh `Idempotency-Key` (a UUID) to every `/api/` POST outside `/api/v1/auth/` that doesn't carry one yet. PUT, PATCH
 * and DELETE are idempotent by design and need none (plan 012). A POST the server marks `[SkipIdempotency]` (e.g. a search)
 * ignores the header, so sending it is harmless.
 * Registered *outside* `authInterceptor`, so the retry after a silent token refresh is a clone of this request and resends the
 * same key: the server then replays instead of booking twice. Double clicks are prevented by the disabled buttons.
 */
export const idempotencyKeyInterceptor: HttpInterceptorFn = (req, next) => {
  const needsKey =
    req.method === 'POST' &&
    req.url.startsWith('/api/') &&
    !req.url.startsWith('/api/v1/auth/') &&
    !req.headers.has(IDEMPOTENCY_KEY_HEADER);
  return next(needsKey ? req.clone({ setHeaders: { [IDEMPOTENCY_KEY_HEADER]: crypto.randomUUID() } }) : req);
};
