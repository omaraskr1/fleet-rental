import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';

import { PlatformAuthStore } from '../stores/platform-auth.store';

/**
 * Attaches the platform admin's bearer token to platform API calls only.
 * The counterpart to authInterceptor, kept separate because the two sessions
 * (tenant user vs. platform admin) are unrelated identities that can be active
 * at the same time in two different browser tabs.
 */
export const platformAuthInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.includes('/platform/')) {
    return next(req);
  }

  const token = inject(PlatformAuthStore).token();

  if (!token) {
    return next(req);
  }

  return next(
    req.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    }),
  );
};
