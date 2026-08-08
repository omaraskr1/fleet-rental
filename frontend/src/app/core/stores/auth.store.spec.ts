import { TestBed } from '@angular/core/testing';

import { AuthStore } from './auth.store';
import { ApiService } from '../services/api.service';
import type { AppUser, AuthResponse } from '../models';

const CLIENT: AppUser = {
  id: 'u1',
  email: 'client@test.com',
  fullName: 'Test Client',
  phoneNumber: null,
  role: 'Client',
};

const ADMIN: AppUser = { ...CLIENT, id: 'a1', email: 'admin@test.com', role: 'Admin' };

function authResponse(user: AppUser): AuthResponse {
  return { accessToken: 'token-123', expiresAt: new Date().toISOString(), user };
}

describe('AuthStore', () => {
  let api: {
    signUp: ReturnType<typeof vi.fn>;
    login: ReturnType<typeof vi.fn>;
    adminLogin: ReturnType<typeof vi.fn>;
    me: ReturnType<typeof vi.fn>;
  };
  let store: AuthStore;

  beforeEach(() => {
    localStorage.clear();

    api = {
      signUp: vi.fn(),
      login: vi.fn(),
      adminLogin: vi.fn(),
      me: vi.fn(),
    };

    TestBed.configureTestingModule({
      providers: [{ provide: ApiService, useValue: api }],
    });

    store = TestBed.inject(AuthStore);
  });

  it('starts signed out', () => {
    expect(store.isAuthenticated()).toBe(false);
    expect(store.user()).toBeNull();
    expect(store.isAdmin()).toBe(false);
  });

  it('login persists the token and user, and marks the session authenticated', async () => {
    api.login.mockResolvedValue(authResponse(CLIENT));

    await store.login('client@test.com', 'pw');

    expect(store.isAuthenticated()).toBe(true);
    expect(store.user()).toEqual(CLIENT);
    expect(store.token()).toBe('token-123');
    expect(localStorage.getItem('fleet_rental_token')).toBe('token-123');
  });

  it('isAdmin reflects the role on the signed-in user, not a guess from the login endpoint used', async () => {
    api.adminLogin.mockResolvedValue(authResponse(ADMIN));

    await store.adminLogin('admin@test.com', 'pw');

    expect(store.isAdmin()).toBe(true);
    expect(store.displayName()).toBe(ADMIN.fullName);
  });

  it('a failed login clears loading, sets a message, and leaves the session signed out', async () => {
    api.login.mockRejectedValue(new Error('Incorrect email or password.'));

    await expect(store.login('x@test.com', 'wrong')).rejects.toThrow();

    expect(store.isAuthenticated()).toBe(false);
    expect(store.error()).toBe('Incorrect email or password.');
    expect(store.loading()).toBe(false);
  });

  it('signOut clears the session and storage so a later restore finds nothing', async () => {
    api.login.mockResolvedValue(authResponse(CLIENT));
    await store.login('client@test.com', 'pw');

    store.signOut();

    expect(store.isAuthenticated()).toBe(false);
    expect(store.user()).toBeNull();
    expect(localStorage.getItem('fleet_rental_token')).toBeNull();
    expect(localStorage.getItem('fleet_rental_user')).toBeNull();
  });

  describe('restoreSession', () => {
    it('does nothing when no token was ever stored', async () => {
      await store.restoreSession();

      expect(store.isAuthenticated()).toBe(false);
      expect(api.me).not.toHaveBeenCalled();
    });

    it('shows the cached user immediately, before the confirmation call resolves', async () => {
      localStorage.setItem('fleet_rental_token', 'stale-token');
      localStorage.setItem('fleet_rental_user', JSON.stringify(CLIENT));

      // Never resolves during this test, so the cached value is the only thing
      // that could have populated the signal at this point.
      api.me.mockReturnValue(new Promise(() => {}));

      const restoring = store.restoreSession();

      expect(store.isAuthenticated()).toBe(true);
      expect(store.user()).toEqual(CLIENT);

      await Promise.race([restoring, Promise.resolve()]);
    });

    it('an expired token is dropped rather than left in a half-signed-in state', async () => {
      localStorage.setItem('fleet_rental_token', 'expired-token');
      localStorage.setItem('fleet_rental_user', JSON.stringify(CLIENT));
      api.me.mockRejectedValue(new Error('Unauthorized'));

      await store.restoreSession();

      expect(store.isAuthenticated()).toBe(false);
      expect(store.user()).toBeNull();
      expect(localStorage.getItem('fleet_rental_token')).toBeNull();
    });

    it('a corrupted cached user is discarded without blocking the confirmation call', async () => {
      localStorage.setItem('fleet_rental_token', 'a-token');
      localStorage.setItem('fleet_rental_user', '{not valid json');
      api.me.mockResolvedValue(CLIENT);

      await store.restoreSession();

      expect(store.user()).toEqual(CLIENT);
    });

    it('a live token is confirmed and its user refreshed from the server', async () => {
      localStorage.setItem('fleet_rental_token', 'a-token');
      const updated: AppUser = { ...CLIENT, fullName: 'Renamed Client' };
      api.me.mockResolvedValue(updated);

      await store.restoreSession();

      expect(store.user()).toEqual(updated);
      expect(localStorage.getItem('fleet_rental_user')).toBe(JSON.stringify(updated));
    });
  });

  it('clearError resets the error without touching the session', async () => {
    api.login.mockRejectedValue(new Error('boom'));
    await expect(store.login('x@test.com', 'pw')).rejects.toThrow();
    expect(store.error()).toBe('boom');

    store.clearError();

    expect(store.error()).toBeNull();
  });
});
