import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, type RouterStateSnapshot } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';

import { tenantGuard } from './tenant.guard';
import { TenantStore } from '../stores/tenant.store';

function stateFor(url: string): RouterStateSnapshot {
  return { url } as RouterStateSnapshot;
}

describe('tenantGuard', () => {
  let router: Router;
  let tenant: TenantStore;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient()],
    });

    router = TestBed.inject(Router);
    tenant = TestBed.inject(TenantStore);
  });

  function activate(url = '/tabs/cars') {
    return TestBed.runInInjectionContext(() => tenantGuard({} as never, stateFor(url)));
  }

  it('blocks access with no company selected and preserves the return URL', () => {
    const result = activate('/tabs/bookings');

    expect(result).not.toBe(true);
    const tree = router.serializeUrl(result as ReturnType<Router['createUrlTree']>);
    expect(tree).toContain('/select-company');
    expect(tree).toContain('returnUrl=%2Ftabs%2Fbookings');
  });

  it('allows the request through once a company has been selected', () => {
    localStorage.setItem(
      'fleet_rental_tenant',
      JSON.stringify({ code: 'gulf-fleet', name: 'Gulf Fleet' }),
    );
    tenant.restore();

    expect(activate()).toBe(true);
  });
});
