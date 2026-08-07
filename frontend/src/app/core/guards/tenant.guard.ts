import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { TenantStore } from '../stores/tenant.store';

/**
 * Requires a company to have been selected before anything else loads.
 *
 * Placed ahead of every other guard in the tree: auth and admin guards check WHO
 * you are, but that question is meaningless until WHICH company has been
 * established first.
 */
export const tenantGuard: CanActivateFn = (_route, state) => {
  const tenant = inject(TenantStore);
  const router = inject(Router);

  if (tenant.isSelected()) {
    return true;
  }

  return router.createUrlTree(['/select-company'], { queryParams: { returnUrl: state.url } });
};
