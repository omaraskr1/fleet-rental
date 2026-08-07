import { Injectable, computed, inject, signal } from '@angular/core';

import { ApiService } from '../services/api.service';
import type { AppUser } from '../models';

const TOKEN_KEY = 'fleet_rental_token';
const USER_KEY = 'fleet_rental_user';

/**
 * Session state. The single source of truth for "who is signed in" — the auth
 * interceptor, the route guards, and the shell layout all read these signals.
 */
@Injectable({ providedIn: 'root' })
export class AuthStore {
  private readonly api = inject(ApiService);

  private readonly _user = signal<AppUser | null>(null);
  private readonly _token = signal<string | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly user = this._user.asReadonly();
  readonly token = this._token.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  readonly isAuthenticated = computed(() => this._token() !== null);
  readonly isAdmin = computed(() => this._user()?.role === 'Admin');
  readonly displayName = computed(() => this._user()?.fullName ?? '');

  /**
   * Rehydrates from storage on cold start, then confirms the token is still
   * valid. Without the server round-trip an expired token would look like a
   * live session until the first API call failed.
   */
  async restoreSession(): Promise<void> {
    const token = localStorage.getItem(TOKEN_KEY);
    const cachedUser = localStorage.getItem(USER_KEY);

    if (!token) {
      return;
    }

    this._token.set(token);

    // Show the cached user immediately so the UI does not flash a signed-out
    // state while the confirmation request is in flight.
    if (cachedUser) {
      try {
        this._user.set(JSON.parse(cachedUser) as AppUser);
      } catch {
        localStorage.removeItem(USER_KEY);
      }
    }

    try {
      const user = await this.api.me();
      this.persist(token, user);
    } catch {
      // Expired or revoked — drop it rather than leaving a token that 401s.
      this.signOut();
    }
  }

  async signUp(email: string, password: string, fullName: string, phone?: string): Promise<void> {
    await this.run(() => this.api.signUp(email, password, fullName, phone));
  }

  async login(email: string, password: string): Promise<void> {
    await this.run(() => this.api.login(email, password));
  }

  async adminLogin(email: string, password: string): Promise<void> {
    await this.run(() => this.api.adminLogin(email, password));
  }

  signOut(): void {
    this._token.set(null);
    this._user.set(null);
    this._error.set(null);
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
  }

  clearError(): void {
    this._error.set(null);
  }

  private async run(operation: () => Promise<{ accessToken: string; user: AppUser }>): Promise<void> {
    this._loading.set(true);
    this._error.set(null);

    try {
      const result = await operation();
      this.persist(result.accessToken, result.user);
    } catch (error) {
      // The error interceptor has already turned this into a readable message.
      this._error.set((error as Error).message);
      throw error;
    } finally {
      this._loading.set(false);
    }
  }

  private persist(token: string, user: AppUser): void {
    this._token.set(token);
    this._user.set(user);
    localStorage.setItem(TOKEN_KEY, token);
    localStorage.setItem(USER_KEY, JSON.stringify(user));
  }
}
