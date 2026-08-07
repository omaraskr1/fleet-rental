import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthStore } from '../stores/auth.store';

/** Requires any signed-in user. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthStore);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return true;
  }

  // Carry the attempted URL so login can return the user where they were headed
  // instead of dumping them on the home tab.
  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/** Requires an administrator. Guards the whole admin section. */
export const adminGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthStore);
  const router = inject(Router);

  if (auth.isAuthenticated() && auth.isAdmin()) {
    return true;
  }

  // A signed-in client hitting an admin URL is not an auth failure — send them
  // to the admin login rather than looping them through the client one.
  return router.createUrlTree(['/admin/login'], { queryParams: { returnUrl: state.url } });
};

/** Keeps signed-in users off the login and signup screens. */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthStore);
  const router = inject(Router);

  if (!auth.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree([auth.isAdmin() ? '/admin' : '/tabs/cars']);
};
