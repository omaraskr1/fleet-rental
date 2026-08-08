import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';

import { TenantStore } from '../stores/tenant.store';

/**
 * Attaches the selected company code to every API call.
 *
 * The backend ignores this header entirely for authenticated requests — there the
 * tenant comes from the signed JWT, which cannot be tampered with. It matters for
 * anonymous calls: no fleet data is served without an account, but signing up and
 * logging in both happen before a token exists, and this is what tells the API
 * whose tenant to sign up or log into.
 */
export const tenantInterceptor: HttpInterceptorFn = (req, next) => {
  const code = inject(TenantStore).code();

  // The tenant lookup itself must not be scoped to a tenant — that is the request
  // that establishes which one to use.
  if (!code || req.url.includes('/tenants/')) {
    return next(req);
  }

  return next(req.clone({ setHeaders: { 'X-Tenant-Code': code } }));
};
