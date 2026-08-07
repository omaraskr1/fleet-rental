import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';

export interface TenantSummary {
  code: string;
  name: string;
}

const TENANT_KEY = 'fleet_rental_tenant';

/**
 * Which rental company this installation belongs to.
 *
 * One app in the store serves every company on the platform, so the client picks
 * theirs once by company code and the choice is remembered. Everything after that
 * — the catalogue, login, bookings — is scoped to it by the API.
 */
@Injectable({ providedIn: 'root' })
export class TenantStore {
  private readonly http = inject(HttpClient);

  private readonly _tenant = signal<TenantSummary | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly tenant = this._tenant.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  readonly code = computed(() => this._tenant()?.code ?? null);
  readonly name = computed(() => this._tenant()?.name ?? '');
  readonly isSelected = computed(() => this._tenant() !== null);

  /** Reads the remembered company on launch, before the first request goes out. */
  restore(): void {
    const stored = localStorage.getItem(TENANT_KEY);

    if (!stored) {
      return;
    }

    try {
      this._tenant.set(JSON.parse(stored) as TenantSummary);
    } catch {
      localStorage.removeItem(TENANT_KEY);
    }
  }

  /**
   * Looks up a code the user typed. Returns false for unknown or suspended
   * companies — the API deliberately does not distinguish the two, so neither
   * does the message shown here.
   */
  async selectByCode(code: string): Promise<boolean> {
    const trimmed = code.trim().toLowerCase();

    if (!trimmed) {
      return false;
    }

    this._loading.set(true);
    this._error.set(null);

    try {
      const tenant = await firstValueFrom(
        this.http.get<TenantSummary>(`${environment.apiBaseUrl}/tenants/${encodeURIComponent(trimmed)}`),
      );

      this._tenant.set(tenant);
      localStorage.setItem(TENANT_KEY, JSON.stringify(tenant));
      return true;
    } catch {
      this._error.set(`We couldn't find a company with the code "${trimmed}".`);
      return false;
    } finally {
      this._loading.set(false);
    }
  }

  /** Clears the company, e.g. when switching to a different rental business. */
  clear(): void {
    this._tenant.set(null);
    this._error.set(null);
    localStorage.removeItem(TENANT_KEY);
  }
}
