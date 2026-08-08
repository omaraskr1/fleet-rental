import { TestBed } from '@angular/core/testing';

import { PlatformAuthStore } from './platform-auth.store';
import { ApiService } from '../services/api.service';
import type { PlatformAdmin, PlatformAuthResponse } from '../models';

const TOKEN_KEY = 'fleet_rental_platform_token';
const ADMIN_KEY = 'fleet_rental_platform_admin';

function admin(overrides: Partial<PlatformAdmin> = {}): PlatformAdmin {
  return { id: 'p1', email: 'root@platform.local', fullName: 'Root Admin', isActive: true, ...overrides };
}

describe('PlatformAuthStore', () => {
  let api: {
    platformLogin: ReturnType<typeof vi.fn>;
    platformMe: ReturnType<typeof vi.fn>;
  };
  let store: PlatformAuthStore;

  beforeEach(() => {
    localStorage.clear();
    api = { platformLogin: vi.fn(), platformMe: vi.fn() };
    TestBed.configureTestingModule({ providers: [{ provide: ApiService, useValue: api }] });
    store = TestBed.inject(PlatformAuthStore);
  });

  it('starts signed out', () => {
    expect(store.isAuthenticated()).toBe(false);
    expect(store.admin()).toBeNull();
  });

  it('login persists the token and admin under platform-specific storage keys', async () => {
    const response: PlatformAuthResponse = {
      accessToken: 'tok-1',
      expiresAt: '2026-12-01T00:00:00Z',
      admin: admin(),
    };
    api.platformLogin.mockResolvedValue(response);

    await store.login('root@platform.local', 'pass');

    expect(store.isAuthenticated()).toBe(true);
    expect(store.admin()).toEqual(admin());
    expect(localStorage.getItem(TOKEN_KEY)).toBe('tok-1');
    expect(JSON.parse(localStorage.getItem(ADMIN_KEY)!)).toEqual(admin());

    // Must never collide with the tenant-user session's own keys.
    expect(localStorage.getItem('fleet_rental_token')).toBeNull();
    expect(localStorage.getItem('fleet_rental_user')).toBeNull();
  });

  it('a failed login surfaces the message and leaves the session signed out', async () => {
    api.platformLogin.mockRejectedValue(new Error('Incorrect email or password.'));

    await expect(store.login('root@platform.local', 'wrong')).rejects.toThrow();

    expect(store.error()).toBe('Incorrect email or password.');
    expect(store.isAuthenticated()).toBe(false);
  });

  it('restoreSession confirms a stored token against the server and refreshes the cached admin', async () => {
    localStorage.setItem(TOKEN_KEY, 'tok-1');
    localStorage.setItem(ADMIN_KEY, JSON.stringify(admin({ fullName: 'Stale Name' })));
    api.platformMe.mockResolvedValue(admin({ fullName: 'Fresh Name' }));

    await store.restoreSession();

    expect(store.isAuthenticated()).toBe(true);
    expect(store.admin()?.fullName).toBe('Fresh Name');
  });

  it('restoreSession signs out when the server rejects the stored token', async () => {
    localStorage.setItem(TOKEN_KEY, 'expired-tok');
    api.platformMe.mockRejectedValue(new Error('Unauthorized'));

    await store.restoreSession();

    expect(store.isAuthenticated()).toBe(false);
    expect(localStorage.getItem(TOKEN_KEY)).toBeNull();
  });

  it('signOut clears the session and storage', async () => {
    api.platformLogin.mockResolvedValue({
      accessToken: 'tok-1',
      expiresAt: '2026-12-01T00:00:00Z',
      admin: admin(),
    });
    await store.login('root@platform.local', 'pass');

    store.signOut();

    expect(store.isAuthenticated()).toBe(false);
    expect(store.admin()).toBeNull();
    expect(localStorage.getItem(TOKEN_KEY)).toBeNull();
  });
});
