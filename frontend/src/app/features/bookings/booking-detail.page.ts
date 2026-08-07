import { Component, inject, input, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import {
  IonBackButton, IonButton, IonButtons, IonCard, IonCardContent, IonContent,
  IonHeader, IonItem, IonLabel, IonList, IonSpinner, IonTitle, IonToolbar,
} from '@ionic/angular/standalone';

import { ApiService } from '../../core/services/api.service';
import { BookingsStore } from '../../core/stores/bookings.store';
import { BookingStatusBadgeComponent } from '../../shared/booking-status-badge.component';
import { humanize, type Booking } from '../../core/models';

@Component({
  selector: 'app-booking-detail',
  imports: [
    IonHeader, IonToolbar, IonTitle, IonButtons, IonBackButton, IonContent,
    IonCard, IonCardContent, IonList, IonItem, IonLabel, IonButton, IonSpinner,
    BookingStatusBadgeComponent, DatePipe,
  ],
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start"><ion-back-button defaultHref="/tabs/bookings" /></ion-buttons>
        <ion-title>Booking</ion-title>
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
              {{ b.startDate | date: 'EEE d MMM' }} – {{ b.endDate | date: 'EEE d MMM yyyy' }}
              <span class="muted">({{ b.totalDays }} days)</span>
            </p>

            @if (b.status === 'Pending') {
              <p class="note">Waiting on the fleet team. You'll be notified once it's decided.</p>
            } @else if (b.decisionReason) {
              <p class="note">{{ b.decisionReason }}</p>
            }
          </ion-card-content>
        </ion-card>

        <ion-card>
          <ion-card-content>
            <h2>Event</h2>
            <ion-list lines="none">
              <ion-item><ion-label><small>Name</small><p>{{ b.event.name }}</p></ion-label></ion-item>
              <ion-item><ion-label><small>Type</small><p>{{ label(b.event.type) }}</p></ion-label></ion-item>
              <ion-item><ion-label><small>Location</small><p>{{ b.event.location }}</p></ion-label></ion-item>
              @if (b.event.expectedAttendance) {
                <ion-item>
                  <ion-label><small>Expected attendance</small><p>{{ b.event.expectedAttendance }}</p></ion-label>
                </ion-item>
              }
              @if (b.event.notes) {
                <ion-item><ion-label><small>Notes</small><p>{{ b.event.notes }}</p></ion-label></ion-item>
              }
            </ion-list>
          </ion-card-content>
        </ion-card>

        @if (b.status === 'Pending' || b.status === 'Approved') {
          <div class="actions">
            <ion-button expand="block" fill="outline" color="danger" (click)="cancel(b.id)">
              Cancel this booking
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

  protected readonly booking = signal<Booking | null>(null);

  async ngOnInit(): Promise<void> {
    this.booking.set(await this.api.getBooking(this.id()));
  }

  protected label(value: string): string {
    return humanize(value);
  }

  protected async cancel(id: string): Promise<void> {
    await this.store.cancel(id);
    this.booking.set(await this.api.getBooking(id));
  }
}
