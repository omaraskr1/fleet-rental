import { Component, computed, input } from '@angular/core';
import { IonBadge } from '@ionic/angular/standalone';

import type { BookingStatus } from '../core/models';

/** One place deciding how each booking status looks, so the two lists agree. */
@Component({
  selector: 'app-booking-status-badge',
  imports: [IonBadge],
  template: `<ion-badge [color]="color()">{{ status() }}</ion-badge>`,
})
export class BookingStatusBadgeComponent {
  readonly status = input.required<BookingStatus>();

  protected readonly color = computed(() => {
    switch (this.status()) {
      case 'Approved':
        return 'success';
      case 'Pending':
        return 'warning';
      case 'Rejected':
        return 'danger';
      default:
        return 'medium';
    }
  });
}
