import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { PlatformAuthStore } from '../stores/platform-auth.store';

/** Requires a signed-in platform admin. Guards the whole platform panel. */
export const platformAuthGuard: CanActivateFn = (_route, state) => {
  const auth = inject(PlatformAuthStore);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/platform/login'], { queryParams: { returnUrl: state.url } });
};

/** Keeps a signed-in platform admin off the platform login screen. */
export const platformGuestGuard: CanActivateFn = () => {
  const auth = inject(PlatformAuthStore);
  const router = inject(Router);

  if (!auth.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/platform']);
};
