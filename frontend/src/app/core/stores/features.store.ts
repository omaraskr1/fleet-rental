import { Injectable, inject, signal } from '@angular/core';

import { ApiService } from '../services/api.service';
import type { FeatureKey, FeatureToggle } from '../models';

/**
 * The signed-in tenant's feature map (feature 5's per-company toggles). Loaded
 * once per admin session and read by the admin shell to hide nav links, and by
 * route guards to keep a disabled feature's URL from opening directly. Hiding the
 * link is only ever a convenience — the backend's `[RequireFeature]` filter is
 * the actual enforcement, so a stale or unloaded map here fails safe (see
 * `isEnabled` below), not open.
 */
@Injectable({ providedIn: 'root' })
export class FeaturesStore {
  private readonly api = inject(ApiService);

  private readonly _toggles = signal<FeatureToggle[]>([]);
  private readonly _loaded = signal(false);

  readonly loaded = this._loaded.asReadonly();

  async load(): Promise<void> {
    try {
      this._toggles.set(await this.api.getFeatures());
    } catch {
      // Swallowed deliberately: isEnabled() already fails safe on an empty map,
      // and the guard that awaits this must not hang or throw just because the
      // feature map couldn't be fetched.
    } finally {
      this._loaded.set(true);
    }
  }

  /**
   * True once the map says so explicitly. Before it has loaded, or for a key it
   * doesn't recognise, this returns false rather than true — a nav link that
   * flashes on then off is a smaller problem than one that opens a page the
   * backend is about to 403.
   */
  isEnabled(key: FeatureKey): boolean {
    if (!this._loaded()) {
      return false;
    }

    return this._toggles().find((t) => t.key === key)?.isEnabled ?? true;
  }
}
