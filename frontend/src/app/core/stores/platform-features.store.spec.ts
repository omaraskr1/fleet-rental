import { TestBed } from '@angular/core/testing';

import { PlatformFeaturesStore } from './platform-features.store';
import { ApiService } from '../services/api.service';
import type { FeatureToggle } from '../models';

describe('PlatformFeaturesStore', () => {
  let api: { getCompanyFeatures: ReturnType<typeof vi.fn>; setCompanyFeature: ReturnType<typeof vi.fn> };
  let store: PlatformFeaturesStore;

  beforeEach(() => {
    api = { getCompanyFeatures: vi.fn(), setCompanyFeature: vi.fn() };
    TestBed.configureTestingModule({ providers: [{ provide: ApiService, useValue: api }] });
    store = TestBed.inject(PlatformFeaturesStore);
  });

  it('starts empty, not loading, with no error', () => {
    expect(store.toggles()).toEqual([]);
    expect(store.loading()).toBe(false);
    expect(store.error()).toBeNull();
  });

  it('load() populates the toggle list for the given company', async () => {
    const toggles: FeatureToggle[] = [
      { key: 'Analytics', isEnabled: true },
      { key: 'Gps', isEnabled: false },
    ];
    api.getCompanyFeatures.mockResolvedValue(toggles);

    await store.load('tenant-1');

    expect(api.getCompanyFeatures).toHaveBeenCalledWith('tenant-1');
    expect(store.toggles()).toEqual(toggles);
    expect(store.loading()).toBe(false);
  });

  it('a failed load surfaces the error and leaves the toggle list empty', async () => {
    api.getCompanyFeatures.mockRejectedValue(new Error('Cannot reach the server.'));

    await store.load('tenant-1');

    expect(store.error()).toBe('Cannot reach the server.');
    expect(store.toggles()).toEqual([]);
    expect(store.loading()).toBe(false);
  });

  it('setEnabled updates only the toggled key, leaving the others untouched', async () => {
    api.getCompanyFeatures.mockResolvedValue([
      { key: 'Analytics', isEnabled: true },
      { key: 'Gps', isEnabled: true },
    ] satisfies FeatureToggle[]);
    await store.load('tenant-1');

    api.setCompanyFeature.mockResolvedValue({ key: 'Gps', isEnabled: false } satisfies FeatureToggle);

    await store.setEnabled('tenant-1', 'Gps', false);

    expect(api.setCompanyFeature).toHaveBeenCalledWith('tenant-1', 'Gps', false);
    expect(store.toggles().find((t) => t.key === 'Gps')?.isEnabled).toBe(false);
    expect(store.toggles().find((t) => t.key === 'Analytics')?.isEnabled).toBe(true);
  });

  it('a failed toggle surfaces the error and rethrows, leaving the list unchanged', async () => {
    const original: FeatureToggle[] = [{ key: 'Gps', isEnabled: true }];
    api.getCompanyFeatures.mockResolvedValue(original);
    await store.load('tenant-1');

    api.setCompanyFeature.mockRejectedValue(new Error('Forbidden.'));

    await expect(store.setEnabled('tenant-1', 'Gps', false)).rejects.toThrow('Forbidden.');

    expect(store.error()).toBe('Forbidden.');
    expect(store.toggles()).toEqual(original);
  });
});
