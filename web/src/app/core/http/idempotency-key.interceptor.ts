import { HttpInterceptorFn } from '@angular/common/http';

/** Spec 009: the header every write carries; the server replays the first response for a repeated key. */
export const IDEMPOTENCY_KEY_HEADER = 'Idempotency-Key';

const READ_METHODS = new Set(['GET', 'HEAD', 'OPTIONS']);

/**
 * Adds a fresh `Idempotency-Key` (a UUID) to every `/api/` write outside `/api/v1/auth/` that doesn't carry one yet.
 * Registered *outside* `authInterceptor`, so the retry after a silent token refresh is a clone of this request and resends the
 * same key: the server then replays instead of booking twice. Double clicks are prevented by the disabled buttons.
 */
export const idempotencyKeyInterceptor: HttpInterceptorFn = (req, next) => {
  const needsKey =
    !READ_METHODS.has(req.method) &&
    req.url.startsWith('/api/') &&
    !req.url.startsWith('/api/v1/auth/') &&
    !req.headers.has(IDEMPOTENCY_KEY_HEADER);
  return next(needsKey ? req.clone({ setHeaders: { [IDEMPOTENCY_KEY_HEADER]: crypto.randomUUID() } }) : req);
};
