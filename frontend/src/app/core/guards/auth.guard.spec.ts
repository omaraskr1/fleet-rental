import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, type RouterStateSnapshot } from '@angular/router';

import { adminGuard, authGuard, guestGuard } from './auth.guard';
import { AuthStore } from '../stores/auth.store';
import { ApiService } from '../services/api.service';
import type { AppUser } from '../models';

const CLIENT_USER: AppUser = {
  id: 'u1',
  email: 'c@test.com',
  fullName: 'Client',
  phoneNumber: null,
  role: 'Client',
};

const ADMIN_USER: AppUser = { ...CLIENT_USER, id: 'a1', role: 'Admin' };

function stateFor(url: string): RouterStateSnapshot {
  return { url } as RouterStateSnapshot;
}

describe('auth guards', () => {
  let auth: AuthStore;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: ApiService, useValue: {} },
      ],
    });

    auth = TestBed.inject(AuthStore);
    router = TestBed.inject(Router);
  });

  function activate(guard: typeof authGuard, url = '/protected') {
    return TestBed.runInInjectionContext(() =>
      guard({} as never, stateFor(url)),
    );
  }

  function signIn(user: AppUser) {
    // Stores persist through localStorage on login; reaching straight for the
    // private signals would couple this test to internals the guard does not
    // see, so drive it through the same session-restore path the app uses.
    localStorage.setItem('fleet_rental_token', 'a-token');
    localStorage.setItem('fleet_rental_user', JSON.stringify(user));
  }

  describe('authGuard', () => {
    it('blocks an anonymous visitor and sends them to login with a returnUrl', () => {
      const result = activate(authGuard, '/tabs/bookings');

      expect(result).not.toBe(true);
      const tree = router.serializeUrl(result as ReturnType<Router['createUrlTree']>);
      expect(tree).toContain('/login');
      expect(tree).toContain('returnUrl=%2Ftabs%2Fbookings');
    });

    it('allows a signed-in client through', async () => {
      signIn(CLIENT_USER);
      TestBed.inject(ApiService).me = vi.fn().mockResolvedValue(CLIENT_USER);
      await auth.restoreSession();

      expect(activate(authGuard)).toBe(true);
    });
  });

  describe('adminGuard', () => {
    it('sends a signed-out visitor to the admin login, not the client one', () => {
      const result = activate(adminGuard, '/admin/requests');

      const tree = router.serializeUrl(result as ReturnType<Router['createUrlTree']>);
      expect(tree).toContain('/admin/login');
    });

    it('sends a signed-in CLIENT to the admin login rather than treating it as a bare auth failure', async () => {
      signIn(CLIENT_USER);
      TestBed.inject(ApiService).me = vi.fn().mockResolvedValue(CLIENT_USER);
      await auth.restoreSession();

      const result = activate(adminGuard);

      expect(result).not.toBe(true);
      const tree = router.serializeUrl(result as ReturnType<Router['createUrlTree']>);
      expect(tree).toContain('/admin/login');
    });

    it('allows a signed-in admin through', async () => {
      signIn(ADMIN_USER);
      TestBed.inject(ApiService).me = vi.fn().mockResolvedValue(ADMIN_USER);
      await auth.restoreSession();

      expect(activate(adminGuard)).toBe(true);
    });
  });

  describe('guestGuard', () => {
    it('allows an anonymous visitor to reach login/signup', () => {
      expect(activate(guestGuard)).toBe(true);
    });

    it('redirects a signed-in client to the fleet tab, not the admin panel', async () => {
      signIn(CLIENT_USER);
      TestBed.inject(ApiService).me = vi.fn().mockResolvedValue(CLIENT_USER);
      await auth.restoreSession();

      const result = activate(guestGuard);

      const tree = router.serializeUrl(result as ReturnType<Router['createUrlTree']>);
      expect(tree).toContain('/tabs/cars');
    });

    it('redirects a signed-in admin to the admin panel, not the client tabs', async () => {
      signIn(ADMIN_USER);
      TestBed.inject(ApiService).me = vi.fn().mockResolvedValue(ADMIN_USER);
      await auth.restoreSession();

      const result = activate(guestGuard);

      const tree = router.serializeUrl(result as ReturnType<Router['createUrlTree']>);
      expect(tree).toContain('/admin');
      expect(tree).not.toContain('/tabs');
    });
  });
});
