import { Component, computed, inject } from '@angular/core';
import {
  IonIcon,
  IonLabel,
  IonRouterOutlet,
  IonTabBar,
  IonTabButton,
  IonTabs,
  IonBadge,
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { calendarOutline, carSportOutline, personOutline } from 'ionicons/icons';

import { AuthStore } from '../../core/stores/auth.store';
import { BookingsStore } from '../../core/stores/bookings.store';

@Component({
  selector: 'app-tabs',
  imports: [IonTabs, IonTabBar, IonTabButton, IonIcon, IonLabel, IonRouterOutlet, IonBadge],
  template: `
    <ion-tabs>
      <ion-router-outlet />

      <ion-tab-bar slot="bottom">
        <ion-tab-button tab="cars" href="/tabs/cars">
          <ion-icon name="car-sport-outline" />
          <ion-label>Fleet</ion-label>
        </ion-tab-button>

        <ion-tab-button tab="bookings" href="/tabs/bookings">
          <ion-icon name="calendar-outline" />
          <ion-label>Bookings</ion-label>
          @if (pendingCount() > 0) {
            <ion-badge color="warning">{{ pendingCount() }}</ion-badge>
          }
        </ion-tab-button>

        <ion-tab-button tab="profile" href="/tabs/profile">
          <ion-icon name="person-outline" />
          <ion-label>{{ isAuthenticated() ? 'Profile' : 'Sign in' }}</ion-label>
        </ion-tab-button>
      </ion-tab-bar>
    </ion-tabs>
  `,
})
export class TabsPage {
  private readonly auth = inject(AuthStore);
  private readonly bookings = inject(BookingsStore);

  readonly isAuthenticated = this.auth.isAuthenticated;

  /** Awaiting-decision count, so a client sees movement without opening the tab. */
  readonly pendingCount = computed(() => this.bookings.awaitingDecision().length);

  constructor() {
    addIcons({ carSportOutline, calendarOutline, personOutline });
  }
}
