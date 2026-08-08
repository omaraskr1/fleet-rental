import { Injectable, computed, inject, signal } from '@angular/core';

import { ApiService } from '../services/api.service';
import type { PlatformAdmin } from '../models';

const TOKEN_KEY = 'fleet_rental_platform_token';
const ADMIN_KEY = 'fleet_rental_platform_admin';

/**
 * Session state for the platform panel. Deliberately separate from
 * {@link AuthStore}, storage keys included — a platform admin's session and a
 * tenant admin's session are unrelated identities and must never collide or be
 * confused by an interceptor reading the wrong one.
 */
@Injectable({ providedIn: 'root' })
export class PlatformAuthStore {
  private readonly api = inject(ApiService);

  private readonly _admin = signal<PlatformAdmin | null>(null);
  private readonly _token = signal<string | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly admin = this._admin.asReadonly();
  readonly token = this._token.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  readonly isAuthenticated = computed(() => this._token() !== null);

  async restoreSession(): Promise<void> {
    const token = localStorage.getItem(TOKEN_KEY);
    const cachedAdmin = localStorage.getItem(ADMIN_KEY);

    if (!token) {
      return;
    }

    this._token.set(token);

    if (cachedAdmin) {
      try {
        this._admin.set(JSON.parse(cachedAdmin) as PlatformAdmin);
      } catch {
        localStorage.removeItem(ADMIN_KEY);
      }
    }

    try {
      const admin = await this.api.platformMe();
      this.persist(token, admin);
    } catch {
      this.signOut();
    }
  }

  async login(email: string, password: string): Promise<void> {
    this._loading.set(true);
    this._error.set(null);

    try {
      const result = await this.api.platformLogin(email, password);
      this.persist(result.accessToken, result.admin);
    } catch (error) {
      this._error.set((error as Error).message);
      throw error;
    } finally {
      this._loading.set(false);
    }
  }

  signOut(): void {
    this._token.set(null);
    this._admin.set(null);
    this._error.set(null);
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(ADMIN_KEY);
  }

  clearError(): void {
    this._error.set(null);
  }

  private persist(token: string, admin: PlatformAdmin): void {
    this._token.set(token);
    this._admin.set(admin);
    localStorage.setItem(TOKEN_KEY, token);
    localStorage.setItem(ADMIN_KEY, JSON.stringify(admin));
  }
}
