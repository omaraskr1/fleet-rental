import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { FeaturesStore } from '../stores/features.store';

/**
 * Keeps a disabled feature's URL from opening directly, on top of the backend's
 * own enforcement (`[RequireFeature]`) — this only makes the UI consistent with
 * what the API would refuse anyway.
 *
 * Async because a deep link (bookmark, refresh) can reach this guard before
 * AdminShellPage has had a chance to load the feature map — awaiting the load
 * here rather than trusting an ngOnInit ordering avoids a race that would
 * otherwise redirect away from a feature that is actually enabled.
 */
function featureGuard(key: 'Analytics' | 'Maintenance' | 'Gps'): CanActivateFn {
  return async () => {
    const features = inject(FeaturesStore);
    const router = inject(Router);

    if (!features.loaded()) {
      await features.load();
    }

    return features.isEnabled(key) || router.createUrlTree(['/admin']);
  };
}

export const analyticsFeatureGuard: CanActivateFn = featureGuard('Analytics');

export const maintenanceFeatureGuard: CanActivateFn = featureGuard('Maintenance');

export const gpsFeatureGuard: CanActivateFn = featureGuard('Gps');
