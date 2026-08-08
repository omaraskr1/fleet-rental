import { Component, inject, input, OnInit, signal } from '@angular/core';
import {
  IonBackButton, IonButton, IonButtons, IonCard, IonCardContent, IonContent,
  IonHeader, IonItem, IonLabel, IonList, IonSpinner, IonTitle, IonToolbar,
} from '@ionic/angular/standalone';

import { ApiService } from '../../core/services/api.service';
import { BookingsStore } from '../../core/stores/bookings.store';
import { LocaleStore } from '../../core/stores/locale.store';
import { BookingStatusBadgeComponent } from '../../shared/booking-status-badge.component';
import type { Booking } from '../../core/models';

@Component({
  selector: 'app-booking-detail',
  imports: [
    IonHeader, IonToolbar, IonTitle, IonButtons, IonBackButton, IonContent,
    IonCard, IonCardContent, IonList, IonItem, IonLabel, IonButton, IonSpinner,
    BookingStatusBadgeComponent,
  ],
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start"><ion-back-button defaultHref="/tabs/bookings" /></ion-buttons>
        <ion-title>{{ locale.t('booking.detail.title') }}</ion-title>
      </ion-toolbar>
    </ion-header>

    <ion-content>
      @if (booking(); as b) {
        <ion-card>
          <ion-card-content>
            <div class="row">
              <h1>{{ b.carName }}</h1>
              <app-booking-status-badge [status]="b.status" />
            </div>
            <p class="dates">
              {{ locale.formatDate(b.startDate, { weekday: 'short', day: 'numeric', month: 'short' }) }} –
              {{ locale.formatDate(b.endDate, { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' }) }}
              <span class="muted">({{ b.totalDays }} {{ locale.t('common.days') }})</span>
            </p>

            @if (b.status === 'Pending') {
              <p class="note">{{ locale.t('booking.detail.waitingNote') }}</p>
            } @else if (b.decisionReason) {
              <p class="note">{{ b.decisionReason }}</p>
            }
          </ion-card-content>
        </ion-card>

        <ion-card>
          <ion-card-content>
            <h2>{{ locale.t('booking.detail.event') }}</h2>
            <ion-list lines="none">
              <ion-item><ion-label><small>{{ locale.t('booking.detail.name') }}</small><p>{{ b.event.name }}</p></ion-label></ion-item>
              <ion-item><ion-label><small>{{ locale.t('booking.detail.type') }}</small><p>{{ eventTypeLabel(b.event.type) }}</p></ion-label></ion-item>
              <ion-item><ion-label><small>{{ locale.t('booking.detail.location') }}</small><p>{{ b.event.location }}</p></ion-label></ion-item>
              @if (b.event.expectedAttendance) {
                <ion-item>
                  <ion-label><small>{{ locale.t('booking.detail.attendance') }}</small><p>{{ b.event.expectedAttendance }}</p></ion-label>
                </ion-item>
              }
              @if (b.event.notes) {
                <ion-item><ion-label><small>{{ locale.t('booking.detail.notes') }}</small><p>{{ b.event.notes }}</p></ion-label></ion-item>
              }
            </ion-list>
          </ion-card-content>
        </ion-card>

        @if (b.status === 'Pending' || b.status === 'Approved') {
          <div class="actions">
            <ion-button expand="block" fill="outline" color="danger" (click)="cancel(b.id)">
              {{ locale.t('booking.detail.cancelButton') }}
            </ion-button>
          </div>
        }
      } @else {
        <div class="state"><ion-spinner /></div>
      }
    </ion-content>
  `,
  styles: `
    .row { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
    h1 { margin: 0; font-size: 1.25rem; }
    h2 { margin: 0 0 8px; font-size: 1rem; }
    .dates { margin: 8px 0 0; font-weight: 500; }
    .muted { color: var(--ion-color-medium); }
    .note { margin: 12px 0 0; color: var(--ion-color-medium); font-size: 0.9rem; }
    small { color: var(--ion-color-medium); font-size: 0.75rem; }
    .actions { padding: 8px 16px 24px; }
    .state { display: flex; justify-content: center; padding: 64px; }
  `,
})
export class BookingDetailPage implements OnInit {
  readonly id = input.required<string>();

  private readonly api = inject(ApiService);
  private readonly store = inject(BookingsStore);
  protected readonly locale = inject(LocaleStore);

  protected readonly booking = signal<Booking | null>(null);

  async ngOnInit(): Promise<void> {
    this.booking.set(await this.api.getBooking(this.id()));
  }

  protected eventTypeLabel(value: string): string {
    return this.locale.t(`enums.eventType.${value}`);
  }

  protected async cancel(id: string): Promise<void> {
    await this.store.cancel(id);
    this.booking.set(await this.api.getBooking(id));
  }
}
