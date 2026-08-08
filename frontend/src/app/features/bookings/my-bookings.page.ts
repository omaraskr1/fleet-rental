import { Component, inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import {
  IonCard, IonCardContent, IonContent, IonHeader, IonIcon, IonRefresher,
  IonRefresherContent, IonSpinner, IonTitle, IonToolbar,
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { calendarClearOutline } from 'ionicons/icons';

import { BookingsStore } from '../../core/stores/bookings.store';
import { LocaleStore } from '../../core/stores/locale.store';
import { BookingStatusBadgeComponent } from '../../shared/booking-status-badge.component';

@Component({
  selector: 'app-my-bookings',
  imports: [
    IonHeader, IonToolbar, IonTitle, IonContent, IonCard, IonCardContent,
    IonIcon, IonRefresher, IonRefresherContent, IonSpinner,
    BookingStatusBadgeComponent,
  ],
  template: `
    <ion-header>
      <ion-toolbar><ion-title>{{ locale.t('booking.my.title') }}</ion-title></ion-toolbar>
    </ion-header>

    <ion-content>
      <ion-refresher slot="fixed" (ionRefresh)="refresh($event)">
        <ion-refresher-content />
      </ion-refresher>

      @if (store.loading() && store.myBookings().length === 0) {
        <div class="state"><ion-spinner /></div>
      } @else if (store.myBookings().length === 0) {
        <div class="state">
          <ion-icon name="calendar-clear-outline" />
          <p>{{ locale.t('booking.my.empty') }}</p>
          <p class="hint">{{ locale.t('booking.my.emptyHint') }}</p>
        </div>
      } @else {
        @for (booking of store.myBookings(); track booking.id) {
          <ion-card button (click)="open(booking.id)">
            <ion-card-content>
              <div class="row">
                <h2>{{ booking.carName }}</h2>
                <app-booking-status-badge [status]="booking.status" />
              </div>
              <p class="dates">
                {{ locale.formatDate(booking.startDate, { day: 'numeric', month: 'short' }) }} –
                {{ locale.formatDate(booking.endDate, { day: 'numeric', month: 'short', year: 'numeric' }) }}
                <span class="muted">({{ booking.totalDays }} {{ locale.t('common.days') }})</span>
              </p>
              <p class="event">{{ booking.event.name }} · {{ booking.event.location }}</p>
              @if (booking.status === 'Rejected' && booking.decisionReason) {
                <p class="reason">{{ booking.decisionReason }}</p>
              }
            </ion-card-content>
          </ion-card>
        }
      }
    </ion-content>
  `,
  styles: `
    .row { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
    h2 { margin: 0; font-size: 1.05rem; }
    .dates { margin: 6px 0 2px; font-weight: 500; }
    .muted, .event { color: var(--ion-color-medium); }
    .event { margin: 0; font-size: 0.9rem; }
    .reason { margin: 8px 0 0; font-size: 0.85rem; color: var(--ion-color-danger); }
    .state { display: flex; flex-direction: column; align-items: center; gap: 4px;
             padding: 64px 24px; color: var(--ion-color-medium); text-align: center; }
    .state ion-icon { font-size: 48px; margin-bottom: 8px; }
    .hint { font-size: 0.85rem; }
  `,
})
export class MyBookingsPage implements OnInit {
  protected readonly store = inject(BookingsStore);
  protected readonly locale = inject(LocaleStore);
  private readonly router = inject(Router);

  constructor() {
    addIcons({ calendarClearOutline });
  }

  ngOnInit(): void {
    void this.store.loadMyBookings();
  }

  protected open(id: string): void {
    void this.router.navigate(['/bookings', id]);
  }

  protected async refresh(event: CustomEvent): Promise<void> {
    await this.store.loadMyBookings();
    (event.target as HTMLIonRefresherElement).complete();
  }
}
