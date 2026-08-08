import { TestBed } from '@angular/core/testing';

import { FeaturesStore } from './features.store';
import { ApiService } from '../services/api.service';
import type { FeatureToggle } from '../models';

describe('FeaturesStore', () => {
  let api: { getFeatures: ReturnType<typeof vi.fn> };
  let store: FeaturesStore;

  beforeEach(() => {
    api = { getFeatures: vi.fn() };
    TestBed.configureTestingModule({ providers: [{ provide: ApiService, useValue: api }] });
    store = TestBed.inject(FeaturesStore);
  });

  it('fails safe (not enabled) for every key before the map has loaded', () => {
    expect(store.loaded()).toBe(false);
    expect(store.isEnabled('Analytics')).toBe(false);
    expect(store.isEnabled('Maintenance')).toBe(false);
  });

  it('reflects the loaded map once load() resolves', async () => {
    const toggles: FeatureToggle[] = [
      { key: 'Analytics', isEnabled: false },
      { key: 'Maintenance', isEnabled: true },
      { key: 'Gps', isEnabled: true },
      { key: 'PushNotifications', isEnabled: true },
    ];
    api.getFeatures.mockResolvedValue(toggles);

    await store.load();

    expect(store.loaded()).toBe(true);
    expect(store.isEnabled('Analytics')).toBe(false);
    expect(store.isEnabled('Maintenance')).toBe(true);
  });

  it('marks itself loaded even if the request fails, so the guard does not hang retrying', async () => {
    api.getFeatures.mockRejectedValue(new Error('Cannot reach the server.'));

    await store.load();

    expect(store.loaded()).toBe(true);
  });
});
