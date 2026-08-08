import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  IonButton, IonCard, IonCardContent, IonNote, IonSegment, IonSegmentButton,
  IonSpinner, IonLabel, IonTextarea,
} from '@ionic/angular/standalone';

import { ApiService } from '../../core/services/api.service';
import { BookingsStore } from '../../core/stores/bookings.store';
import { LocaleStore } from '../../core/stores/locale.store';
import { BookingStatusBadgeComponent } from '../../shared/booking-status-badge.component';
import type { BookingApprovalPredictions, BookingStatus } from '../../core/models';

/** Feature 4 — the approve/reject queue. */
@Component({
  selector: 'app-admin-requests',
  imports: [
    IonSegment, IonSegmentButton, IonLabel, IonCard, IonCardContent, IonButton,
    IonTextarea, IonNote, IonSpinner, BookingStatusBadgeComponent, FormsModule,
  ],
  template: `
    <h1>{{ locale.t('admin.requests.title') }}</h1>

    <ion-segment [value]="store.statusFilter() ?? 'all'" (ionChange)="onFilter($event)">
      <ion-segment-button value="all"><ion-label>{{ locale.t('admin.requests.filterAll') }}</ion-label></ion-segment-button>
      <ion-segment-button value="Pending"><ion-label>{{ locale.t('admin.requests.filterPending') }}</ion-label></ion-segment-button>
      <ion-segment-button value="Approved"><ion-label>{{ locale.t('admin.requests.filterApproved') }}</ion-label></ion-segment-button>
      <ion-segment-button value="Rejected"><ion-label>{{ locale.t('admin.requests.filterRejected') }}</ion-label></ion-segment-button>
    </ion-segment>

    @if (store.error(); as error) {
      <ion-note color="danger" class="banner">{{ error }}</ion-note>
    }

    @if (predictions(); as p) {
      @if (p.hasSufficientData) {
        <div class="toolbar">
          <ion-button size="small" [fill]="sortByLikelihood() ? 'solid' : 'outline'"
                      (click)="toggleSort()">
            {{ locale.t('admin.requests.sortByLikelihood') }}
          </ion-button>
        </div>
      } @else {
        <ion-note class="banner learning">
          {{ locale.t('admin.requests.predictionLearning', {
               minimum: p.minimumRequired, count: p.trainedOnBookings }) }}
        </ion-note>
      }
    }

    @if (store.loading() && store.allBookings().length === 0) {
      <div class="state"><ion-spinner /></div>
    } @else if (store.filteredBookings().length === 0) {
      <p class="state">{{ locale.t('admin.requests.empty') }}</p>
    } @else {
      @for (booking of visibleBookings(); track booking.id) {
        <ion-card>
          <ion-card-content>
            <div class="head">
              <div>
                <h2>{{ booking.carName }}</h2>
                <p class="dates">
                  {{ locale.formatDate(booking.startDate, { day: 'numeric', month: 'short' }) }} –
                  {{ locale.formatDate(booking.endDate, { day: 'numeric', month: 'short', year: 'numeric' }) }}
                  <span class="muted">({{ booking.totalDays }} {{ locale.t('common.days') }})</span>
                </p>
              </div>
              <div class="head-badges">
                @if (booking.status === 'Pending' && probabilityFor(booking.id) !== null) {
                  <span class="likelihood" [class]="likelihoodClass(probabilityFor(booking.id)!)"
                        [title]="locale.t('admin.requests.predictionTooltip', {
                                  percent: asPercent(probabilityFor(booking.id)!) })">
                    {{ likelihoodLabel(probabilityFor(booking.id)!) }}
                    <strong>{{ asPercent(probabilityFor(booking.id)!) }}%</strong>
                  </span>
                }
                <app-booking-status-badge [status]="booking.status" />
              </div>
            </div>

            <div class="grid">
              <div><small>{{ locale.t('admin.requests.client') }}</small><p>{{ booking.clientName }}</p>
                   <p class="muted">{{ booking.clientEmail }}</p></div>
              <div><small>{{ locale.t('admin.requests.event') }}</small><p>{{ booking.event.name }}</p>
                   <p class="muted">{{ eventTypeLabel(booking.event.type) }} · {{ booking.event.location }}</p></div>
              @if (booking.event.expectedAttendance) {
                <div><small>{{ locale.t('admin.requests.attendance') }}</small><p>{{ booking.event.expectedAttendance }}</p></div>
              }
            </div>

            @if (booking.clientNotes) {
              <p class="notes">"{{ booking.clientNotes }}"</p>
            }

            @if (booking.status === 'Pending') {
              @if (decidingId() === booking.id) {
                <ion-textarea
                  [(ngModel)]="reason"
                  [placeholder]="locale.t('admin.requests.reasonPlaceholder')"
                  [autoGrow]="true"
                  fill="outline" />
                <div class="actions">
                  <ion-button size="small" color="success"
                              [disabled]="store.submitting()"
                              (click)="approve(booking.id)">{{ locale.t('admin.requests.confirmApprove') }}</ion-button>
                  <ion-button size="small" color="danger" fill="outline"
                              [disabled]="store.submitting()"
                              (click)="reject(booking.id)">{{ locale.t('admin.requests.confirmReject') }}</ion-button>
                  <ion-button size="small" fill="clear" (click)="cancelDecision()">{{ locale.t('admin.requests.cancel') }}</ion-button>
                </div>
              } @else {
                <div class="actions">
                  <ion-button size="small" (click)="startDecision(booking.id)">
                    {{ locale.t('admin.requests.approveReject') }}
                  </ion-button>
                </div>
              }
            } @else if (booking.decisionReason) {
              <p class="notes muted">{{ locale.t('admin.requests.decisionNote', { reason: booking.decisionReason }) }}</p>
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
    .banner.learning { color: var(--ion-color-medium); font-size: 0.82rem; }
    .state { padding: 48px; text-align: center; color: var(--ion-color-medium); }
    .toolbar { display: flex; justify-content: flex-end; margin: 8px 0 4px; }
    .head-badges { display: flex; align-items: center; gap: 8px; flex-shrink: 0; flex-wrap: wrap; }
    .likelihood { display: inline-flex; align-items: center; gap: 5px; white-space: nowrap;
                  font-size: 0.72rem; padding: 3px 9px; border-radius: 11px; cursor: default; }
    .likelihood.high { background: var(--ion-color-success-tint); color: var(--ion-color-success-shade); }
    .likelihood.mid { background: var(--ion-color-warning-tint); color: var(--ion-color-warning-shade); }
    .likelihood.low { background: var(--ion-color-danger-tint); color: var(--ion-color-danger-shade); }
  `,
})
export class AdminRequestsPage implements OnInit {
  protected readonly store = inject(BookingsStore);
  protected readonly locale = inject(LocaleStore);
  private readonly api = inject(ApiService);

  /** Which row has its decision panel open. Null means none. */
  protected readonly decidingId = signal<string | null>(null);
  protected reason = '';

  protected readonly predictions = signal<BookingApprovalPredictions | null>(null);
  protected readonly sortByLikelihood = signal(false);

  private readonly probabilityByBookingId = computed(() => {
    const map = new Map<string, number>();
    for (const p of this.predictions()?.predictions ?? []) {
      map.set(p.bookingId, p.approvalProbability);
    }
    return map;
  });

  /**
   * Sorting is opt-in and only ever reorders — no request is hidden by a low
   * score. A prediction is a hint about where to look first, not a filter that
   * quietly buries someone's booking.
   */
  protected readonly visibleBookings = computed(() => {
    const bookings = this.store.filteredBookings();
    if (!this.sortByLikelihood()) {
      return bookings;
    }
    const scores = this.probabilityByBookingId();
    // Unscored rows (anything already decided) sort last rather than jumbling in
    // among the pending ones at an arbitrary position.
    return [...bookings].sort((a, b) => (scores.get(b.id) ?? -1) - (scores.get(a.id) ?? -1));
  });

  ngOnInit(): void {
    void this.store.loadAllBookings();
    void this.loadPredictions();
  }

  /**
   * Predictions are a nice-to-have layered over the queue: if the call fails the
   * admin still gets their requests, just without the badges.
   */
  private async loadPredictions(): Promise<void> {
    try {
      this.predictions.set(await this.api.getBookingApprovalPredictions());
    } catch {
      this.predictions.set(null);
    }
  }

  protected probabilityFor(bookingId: string): number | null {
    return this.probabilityByBookingId().get(bookingId) ?? null;
  }

  protected asPercent(probability: number): number {
    return Math.round(probability * 100);
  }

  protected likelihoodClass(probability: number): string {
    return probability >= 0.66 ? 'high' : probability >= 0.33 ? 'mid' : 'low';
  }

  protected likelihoodLabel(probability: number): string {
    const key = probability >= 0.66 ? 'likelyApprove' : probability >= 0.33 ? 'uncertain' : 'likelyReject';
    return this.locale.t(`admin.requests.${key}`);
  }

  protected toggleSort(): void {
    this.sortByLikelihood.update((on) => !on);
  }

  protected eventTypeLabel(value: string): string {
    return this.locale.t(`enums.eventType.${value}`);
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
      // The decision just made is one more training row, and may be the one that
      // tips a learning fleet over the threshold.
      void this.loadPredictions();
    } catch {
      // A 409 means the dates were taken; the store already refetched the queue,
      // so leaving the panel open lets the admin see the updated row.
    }
  }

  protected async reject(id: string): Promise<void> {
    try {
      await this.store.reject(id, this.reason || undefined);
      this.cancelDecision();
      void this.loadPredictions();
    } catch {
      // Message shown in the banner.
    }
  }
}
