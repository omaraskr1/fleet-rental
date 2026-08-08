import { Injectable, inject, signal } from '@angular/core';

import { ApiService } from '../services/api.service';
import type { FeatureKey, FeatureToggle } from '../models';

/** Per-company feature toggles, managed from the platform panel. */
@Injectable({ providedIn: 'root' })
export class PlatformFeaturesStore {
  private readonly api = inject(ApiService);

  private readonly _toggles = signal<FeatureToggle[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly toggles = this._toggles.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  async load(tenantId: string): Promise<void> {
    this._loading.set(true);
    this._error.set(null);

    try {
      this._toggles.set(await this.api.getCompanyFeatures(tenantId));
    } catch (error) {
      this._error.set((error as Error).message);
    } finally {
      this._loading.set(false);
    }
  }

  async setEnabled(tenantId: string, key: FeatureKey, isEnabled: boolean): Promise<void> {
    this._error.set(null);

    try {
      const updated = await this.api.setCompanyFeature(tenantId, key, isEnabled);
      this._toggles.set(this._toggles().map((t) => (t.key === key ? updated : t)));
    } catch (error) {
      this._error.set((error as Error).message);
      throw error;
    }
  }
}
