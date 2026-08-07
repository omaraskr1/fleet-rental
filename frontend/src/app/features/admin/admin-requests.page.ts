import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  IonButton, IonCard, IonCardContent, IonNote, IonSegment, IonSegmentButton,
  IonSpinner, IonLabel, IonTextarea,
} from '@ionic/angular/standalone';

import { BookingsStore } from '../../core/stores/bookings.store';
import { BookingStatusBadgeComponent } from '../../shared/booking-status-badge.component';
import { humanize, type BookingStatus } from '../../core/models';

/** Feature 4 — the approve/reject queue. */
@Component({
  selector: 'app-admin-requests',
  imports: [
    IonSegment, IonSegmentButton, IonLabel, IonCard, IonCardContent, IonButton,
    IonTextarea, IonNote, IonSpinner, BookingStatusBadgeComponent, DatePipe, FormsModule,
  ],
  template: `
    <h1>Booking requests</h1>

    <ion-segment [value]="store.statusFilter() ?? 'all'" (ionChange)="onFilter($event)">
      <ion-segment-button value="all"><ion-label>All</ion-label></ion-segment-button>
      <ion-segment-button value="Pending"><ion-label>Pending</ion-label></ion-segment-button>
      <ion-segment-button value="Approved"><ion-label>Approved</ion-label></ion-segment-button>
      <ion-segment-button value="Rejected"><ion-label>Rejected</ion-label></ion-segment-button>
    </ion-segment>

    @if (store.error(); as error) {
      <ion-note color="danger" class="banner">{{ error }}</ion-note>
    }

    @if (store.loading() && store.allBookings().length === 0) {
      <div class="state"><ion-spinner /></div>
    } @else if (store.filteredBookings().length === 0) {
      <p class="state">Nothing here.</p>
    } @else {
      @for (booking of store.filteredBookings(); track booking.id) {
        <ion-card>
          <ion-card-content>
            <div class="head">
              <div>
                <h2>{{ booking.carName }}</h2>
                <p class="dates">
                  {{ booking.startDate | date: 'd MMM' }} –
                  {{ booking.endDate | date: 'd MMM yyyy' }}
                  <span class="muted">({{ booking.totalDays }} days)</span>
                </p>
              </div>
              <app-booking-status-badge [status]="booking.status" />
            </div>

            <div class="grid">
              <div><small>Client</small><p>{{ booking.clientName }}</p>
                   <p class="muted">{{ booking.clientEmail }}</p></div>
              <div><small>Event</small><p>{{ booking.event.name }}</p>
                   <p class="muted">{{ label(booking.event.type) }} · {{ booking.event.location }}</p></div>
              @if (booking.event.expectedAttendance) {
                <div><small>Attendance</small><p>{{ booking.event.expectedAttendance }}</p></div>
              }
            </div>

            @if (booking.clientNotes) {
              <p class="notes">"{{ booking.clientNotes }}"</p>
            }

            @if (booking.status === 'Pending') {
              @if (decidingId() === booking.id) {
                <ion-textarea
                  [(ngModel)]="reason"
                  placeholder="Reason (shown to the client in their notification)"
                  [autoGrow]="true"
                  fill="outline" />
                <div class="actions">
                  <ion-button size="small" color="success"
                              [disabled]="store.submitting()"
                              (click)="approve(booking.id)">Confirm approve</ion-button>
                  <ion-button size="small" color="danger" fill="outline"
                              [disabled]="store.submitting()"
                              (click)="reject(booking.id)">Confirm reject</ion-button>
                  <ion-button size="small" fill="clear" (click)="cancelDecision()">Cancel</ion-button>
                </div>
              } @else {
                <div class="actions">
                  <ion-button size="small" (click)="startDecision(booking.id)">
                    Approve / reject
                  </ion-button>
                </div>
              }
            } @else if (booking.decisionReason) {
              <p class="notes muted">Decision note: {{ booking.decisionReason }}</p>
            }
          </ion-card-content>
        </ion-card>
      }
    }
  `,
  styles: `
    h1 { font-size: 1.4rem; margin: 0 0 16px; }
    h2 { margin: 0; font-size: 1.05rem; }
    .head { display: flex; justify-content: space-between; align-items: flex-start; gap: 12px; }
    .dates { margin: 4px 0 0; font-weight: 500; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
            gap: 12px; margin-top: 14px; }
    .grid p { margin: 2px 0; }
    small { color: var(--ion-color-medium); font-size: 0.72rem; text-transform: uppercase; }
    .muted { color: var(--ion-color-medium); font-size: 0.85rem; }
    .notes { margin-top: 12px; font-style: italic; color: var(--ion-color-medium); }
    .actions { display: flex; gap: 8px; margin-top: 14px; flex-wrap: wrap; }
    .banner { display: block; padding: 12px 0; white-space: pre-line; }
    .state { padding: 48px; text-align: center; color: var(--ion-color-medium); }
  `,
})
export class AdminRequestsPage implements OnInit {
  protected readonly store = inject(BookingsStore);

  /** Which row has its decision panel open. Null means none. */
  protected readonly decidingId = signal<string | null>(null);
  protected reason = '';

  ngOnInit(): void {
    void this.store.loadAllBookings();
  }

  protected label(value: string): string {
    return humanize(value);
  }

  protected onFilter(event: CustomEvent): void {
    const value = event.detail.value as string;
    this.store.setStatusFilter(value === 'all' ? null : (value as BookingStatus));
  }

  protected startDecision(id: string): void {
    this.decidingId.set(id);
    this.reason = '';
    this.store.clearError();
  }

  protected cancelDecision(): void {
    this.decidingId.set(null);
    this.reason = '';
  }

  protected async approve(id: string): Promise<void> {
    try {
      await this.store.approve(id, this.reason || undefined);
      this.cancelDecision();
    } catch {
      // A 409 means the dates were taken; the store already refetched the queue,
      // so leaving the panel open lets the admin see the updated row.
    }
  }

  protected async reject(id: string): Promise<void> {
    try {
      await this.store.reject(id, this.reason || undefined);
      this.cancelDecision();
    } catch {
      // Message shown in the banner.
    }
  }
}
