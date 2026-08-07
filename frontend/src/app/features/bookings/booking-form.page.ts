import { Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  IonBackButton, IonButton, IonButtons, IonCard, IonCardContent, IonContent,
  IonFooter, IonHeader, IonInput, IonItem, IonLabel, IonNote, IonSelect,
  IonSelectOption, IonSpinner, IonTextarea, IonTitle, IonToolbar,
} from '@ionic/angular/standalone';

import { AvailabilityCalendarComponent } from '../../shared/availability-calendar.component';
import { BookingsStore } from '../../core/stores/bookings.store';
import { CarsStore } from '../../core/stores/cars.store';
import { EVENT_TYPES, humanize, type EventType, type IsoDate } from '../../core/models';

/** Feature 3 — the booking request form. */
@Component({
  selector: 'app-booking-form',
  imports: [
    IonHeader, IonToolbar, IonTitle, IonButtons, IonBackButton, IonContent,
    IonCard, IonCardContent, IonItem, IonLabel, IonInput, IonSelect,
    IonSelectOption, IonTextarea, IonButton, IonFooter, IonNote, IonSpinner,
    FormsModule, AvailabilityCalendarComponent,
  ],
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start"><ion-back-button [defaultHref]="'/cars/' + id()" /></ion-buttons>
        <ion-title>Request booking</ion-title>
      </ion-toolbar>
    </ion-header>

    <ion-content>
      <ion-card>
        <ion-card-content>
          <h2>1. Pick your dates</h2>
          <p class="hint">
            Tap a start date, then an end date. Booked days can't be selected.
          </p>

          <app-availability-calendar
            [bookedDates]="cars.bookedDateSet()"
            [pendingDates]="cars.pendingDateSet()"
            [selectable]="true"
            [rangeStart]="startDate()"
            [rangeEnd]="endDate()"
            (dateClicked)="pickDate($event)" />

          @if (startDate()) {
            <ion-note class="selection">
              {{ startDate() }}@if (endDate()) { → {{ endDate() }} ({{ totalDays() }} days) }
            </ion-note>
          }
        </ion-card-content>
      </ion-card>

      <ion-card>
        <ion-card-content>
          <h2>2. Tell us about the event</h2>

          <ion-item>
            <ion-label position="stacked">Event name *</ion-label>
            <ion-input
              [(ngModel)]="eventName"
              placeholder="Autumn Product Launch"
              autocapitalize="words" />
          </ion-item>

          <ion-item>
            <ion-label position="stacked">Event type</ion-label>
            <ion-select [(ngModel)]="eventType" interface="action-sheet">
              @for (type of eventTypes; track type) {
                <ion-select-option [value]="type">{{ label(type) }}</ion-select-option>
              }
            </ion-select>
          </ion-item>

          <ion-item>
            <ion-label position="stacked">Location *</ion-label>
            <ion-input [(ngModel)]="location" placeholder="Dubai World Trade Centre" />
          </ion-item>

          <ion-item>
            <ion-label position="stacked">Expected attendance</ion-label>
            <ion-input type="number" [(ngModel)]="attendance" placeholder="250" />
          </ion-item>

          <ion-item>
            <ion-label position="stacked">Anything we should know?</ion-label>
            <ion-textarea
              [(ngModel)]="notes"
              [autoGrow]="true"
              placeholder="Branding, driver requirements, access constraints…" />
          </ion-item>
        </ion-card-content>
      </ion-card>

      @if (bookings.error(); as error) {
        <ion-note color="danger" class="error">{{ error }}</ion-note>
      }
    </ion-content>

    <ion-footer>
      <ion-toolbar>
        <ion-button
          expand="block"
          [disabled]="!isValid() || bookings.submitting()"
          (click)="submit()">
          @if (bookings.submitting()) {
            <ion-spinner name="dots" />
          } @else {
            Submit request
          }
        </ion-button>
      </ion-toolbar>
    </ion-footer>
  `,
  styles: `
    h2 { margin: 0 0 4px; font-size: 1rem; }
    .hint { color: var(--ion-color-medium); font-size: 0.85rem; margin: 0 0 12px; }
    .selection { display: block; margin-top: 12px; text-align: center; font-weight: 600; }
    .error { display: block; padding: 0 16px 16px; white-space: pre-line; }
    ion-footer ion-toolbar { padding: 8px; }
  `,
})
export class BookingFormPage implements OnInit {
  readonly id = input.required<string>();

  protected readonly cars = inject(CarsStore);
  protected readonly bookings = inject(BookingsStore);
  private readonly router = inject(Router);

  protected readonly eventTypes = EVENT_TYPES;

  protected readonly startDate = signal<IsoDate | null>(null);
  protected readonly endDate = signal<IsoDate | null>(null);

  protected eventName = '';
  protected eventType: EventType = 'ProductLaunch';
  protected location = '';
  protected attendance: number | null = null;
  protected notes = '';

  protected readonly totalDays = computed(() => {
    const start = this.startDate();
    const end = this.endDate();
    if (!start || !end) return 0;
    return Math.round((Date.parse(end) - Date.parse(start)) / 86_400_000) + 1;
  });

  protected readonly isValid = computed(
    () =>
      this.startDate() !== null &&
      this.endDate() !== null &&
      this.eventName.trim().length > 0 &&
      this.location.trim().length > 0,
  );

  ngOnInit(): void {
    void this.cars.loadAvailability(this.id());
    this.bookings.clearError();
  }

  protected label(value: string): string {
    return humanize(value);
  }

  /**
   * First tap sets the start; second sets the end. Tapping a date before the
   * current start restarts the range there — less fiddly than forcing the user
   * to clear it first.
   */
  protected pickDate(date: IsoDate): void {
    const start = this.startDate();

    if (!start || this.endDate() || date < start) {
      this.startDate.set(date);
      this.endDate.set(null);
      return;
    }

    this.endDate.set(date);
  }

  protected async submit(): Promise<void> {
    if (!this.isValid()) return;

    try {
      const booking = await this.bookings.submit({
        carId: this.id(),
        startDate: this.startDate()!,
        endDate: this.endDate()!,
        eventName: this.eventName.trim(),
        eventType: this.eventType,
        eventLocation: this.location.trim(),
        expectedAttendance: this.attendance,
        eventNotes: this.notes.trim() || null,
      });

      await this.router.navigate(['/bookings', booking.id], { replaceUrl: true });
    } catch {
      // The store surfaced the message; keep the form open so nothing is lost.
    }
  }
}
