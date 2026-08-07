import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Capacitor } from '@capacitor/core';
import { Device } from '@capacitor/device';
import { PushNotifications } from '@capacitor/push-notifications';

import { ApiService } from './api.service';
import type { DevicePlatform } from '../models';

/**
 * Native push registration (feature 6). Registers the device with the backend so
 * booking decisions can reach the user's phone.
 */
@Injectable({ providedIn: 'root' })
export class PushService {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  private registered = false;

  async register(): Promise<void> {
    // On the web build there is no native push bridge, and the admin panel runs
    // there — so this is a normal no-op, not a failure.
    if (!Capacitor.isNativePlatform() || this.registered) {
      return;
    }

    const permission = await PushNotifications.requestPermissions();

    if (permission.receive !== 'granted') {
      return;
    }

    this.attachListeners();
    await PushNotifications.register();
    this.registered = true;
  }

  /** Called on sign-out so the device stops receiving another user's bookings. */
  async unregister(): Promise<void> {
    if (!Capacitor.isNativePlatform() || !this.registered) {
      return;
    }

    try {
      const { identifier } = await Device.getId();
      await this.api.unregisterDevice(identifier);
      await PushNotifications.removeAllListeners();
    } finally {
      this.registered = false;
    }
  }

  private attachListeners(): void {
    // Fires once the OS issues a token, and again whenever it rotates.
    PushNotifications.addListener('registration', async ({ value }) => {
      const { identifier } = await Device.getId();
      await this.api.registerDevice(value, this.platform(), identifier);
    });

    PushNotifications.addListener('registrationError', (error) => {
      console.error('Push registration failed', error);
    });

    // Tapping a booking-decision notification deep-links to that booking. The
    // backend puts the route in the payload precisely for this.
    PushNotifications.addListener('pushNotificationActionPerformed', (action) => {
      const route = action.notification.data?.['route'] as string | undefined;
      if (route) {
        void this.router.navigateByUrl(route);
      }
    });
  }

  private platform(): DevicePlatform {
    switch (Capacitor.getPlatform()) {
      case 'ios':
        return 'Ios';
      case 'android':
        return 'Android';
      default:
        return 'Web';
    }
  }
}
