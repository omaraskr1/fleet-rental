import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';

import { AuthStore } from '../stores/auth.store';

/**
 * Attaches the bearer token to API calls.
 *
 * Reads the token from the store rather than from localStorage so a sign-out
 * takes effect on in-flight navigation immediately.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(AuthStore).token();

  if (!token) {
    return next(req);
  }

  return next(
    req.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    }),
  );
};
