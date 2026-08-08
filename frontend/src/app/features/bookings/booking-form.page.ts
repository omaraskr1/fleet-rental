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
import { LocaleStore } from '../../core/stores/locale.store';
import { EVENT_TYPES, type EventType, type IsoDate } from '../../core/models';

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
        <ion-title>{{ locale.t('booking.form.title') }}</ion-title>
      </ion-toolbar>
    </ion-header>

    <ion-content>
      <ion-card>
        <ion-card-content>
          <h2>{{ locale.t('booking.form.step1Title') }}</h2>
          <p class="hint">{{ locale.t('booking.form.step1Hint') }}</p>

          <app-availability-calendar
            [bookedDates]="cars.bookedDateSet()"
            [pendingDates]="cars.pendingDateSet()"
            [selectable]="true"
            [rangeStart]="startDate()"
            [rangeEnd]="endDate()"
            (dateClicked)="pickDate($event)" />

          @if (startDate()) {
            <ion-note class="selection">
              {{ locale.formatDate(startDate()!, { day: 'numeric', month: 'short' }) }}
              @if (endDate()) {
                →
                {{ locale.formatDate(endDate()!, { day: 'numeric', month: 'short' }) }}
                ({{ totalDays() }} {{ locale.t('common.days') }})
              }
            </ion-note>
          }
        </ion-card-content>
      </ion-card>

      <ion-card>
        <ion-card-content>
          <h2>{{ locale.t('booking.form.step2Title') }}</h2>

          <ion-item>
            <ion-label position="stacked">{{ locale.t('booking.form.eventName') }}</ion-label>
            <ion-input
              [(ngModel)]="eventName"
              [placeholder]="locale.t('booking.form.eventNamePlaceholder')"
              autocapitalize="words" />
          </ion-item>

          <ion-item>
            <ion-label position="stacked">{{ locale.t('booking.form.eventType') }}</ion-label>
            <ion-select [(ngModel)]="eventType" interface="action-sheet">
              @for (type of eventTypes; track type) {
                <ion-select-option [value]="type">{{ eventTypeLabel(type) }}</ion-select-option>
              }
            </ion-select>
          </ion-item>

          <ion-item>
            <ion-label position="stacked">{{ locale.t('booking.form.location') }}</ion-label>
            <ion-input [(ngModel)]="location" [placeholder]="locale.t('booking.form.locationPlaceholder')" />
          </ion-item>

          <ion-item>
            <ion-label position="stacked">{{ locale.t('booking.form.attendance') }}</ion-label>
            <ion-input type="number" [(ngModel)]="attendance" [placeholder]="locale.t('booking.form.attendancePlaceholder')" />
          </ion-item>

          <ion-item>
            <ion-label position="stacked">{{ locale.t('booking.form.notes') }}</ion-label>
            <ion-textarea
              [(ngModel)]="notes"
              [autoGrow]="true"
              [placeholder]="locale.t('booking.form.notesPlaceholder')" />
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
            {{ locale.t('booking.form.submit') }}
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
  protected readonly locale = inject(LocaleStore);
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

  /**
   * A plain method, not computed(): eventName/location are ngModel-bound
   * plain fields, not signals, so a computed() here would cache its result
   * against only startDate/endDate and never notice text typed afterward.
   */
  protected isValid(): boolean {
    return (
      this.startDate() !== null &&
      this.endDate() !== null &&
      this.eventName.trim().length > 0 &&
      this.location.trim().length > 0
    );
  }

  ngOnInit(): void {
    void this.cars.loadAvailability(this.id());
    this.bookings.clearError();
  }

  protected eventTypeLabel(value: string): string {
    return this.locale.t(`enums.eventType.${value}`);
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
