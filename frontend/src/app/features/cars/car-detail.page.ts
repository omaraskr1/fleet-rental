import { Component, computed, inject, input, OnInit } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { Router } from '@angular/router';
import {
  IonBackButton, IonBadge, IonButton, IonButtons, IonCard, IonCardContent,
  IonContent, IonFooter, IonHeader, IonImg, IonTitle, IonToolbar,
} from '@ionic/angular/standalone';

import { AvailabilityCalendarComponent } from '../../shared/availability-calendar.component';
import { CarsStore } from '../../core/stores/cars.store';
import { humanize } from '../../core/models';

/** Feature 2 — car detail with its availability calendar. */
@Component({
  selector: 'app-car-detail',
  imports: [
    IonHeader, IonToolbar, IonTitle, IonButtons, IonBackButton, IonContent,
    IonCard, IonCardContent, IonImg, IonBadge, IonButton, IonFooter,
    AvailabilityCalendarComponent, CurrencyPipe,
  ],
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start"><ion-back-button defaultHref="/tabs/cars" /></ion-buttons>
        <ion-title>{{ car()?.name ?? 'Vehicle' }}</ion-title>
      </ion-toolbar>
    </ion-header>

    <ion-content>
      @if (car(); as detail) {
        @if (detail.photos.length > 0) {
          <ion-img [src]="detail.photos[0].url" [alt]="detail.name" class="hero" />
        }

        <ion-card>
          <ion-card-content>
            <div class="row">
              <h1>{{ detail.name }}</h1>
              <ion-badge [color]="detail.status === 'Active' ? 'success' : 'medium'">
                {{ label(detail.status) }}
              </ion-badge>
            </div>
            <p class="meta">
              {{ label(detail.category) }} · {{ detail.seats }} seats ·
              <strong>{{ detail.dailyRate | currency: 'USD' : 'symbol' : '1.0-0' }}/day</strong>
            </p>
            @if (detail.description) {
              <p>{{ detail.description }}</p>
            }
          </ion-card-content>
        </ion-card>

        <ion-card>
          <ion-card-content>
            <h2>Availability</h2>
            <app-availability-calendar
              [bookedDates]="store.bookedDateSet()"
              [pendingDates]="store.pendingDateSet()" />
          </ion-card-content>
        </ion-card>
      } @else if (store.error()) {
        <p class="state">{{ store.error() }}</p>
      }
    </ion-content>

    <ion-footer>
      <ion-toolbar>
        <ion-button
          expand="block"
          [disabled]="!canBook()"
          (click)="book()">
          {{ canBook() ? 'Request this vehicle' : 'Not available for booking' }}
        </ion-button>
      </ion-toolbar>
    </ion-footer>
  `,
  styles: `
    .hero { width: 100%; height: 240px; object-fit: cover; }
    .row { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
    h1 { margin: 0; font-size: 1.3rem; }
    h2 { margin: 0 0 12px; font-size: 1rem; }
    .meta { color: var(--ion-color-medium); margin: 6px 0 12px; }
    .state { padding: 32px; text-align: center; color: var(--ion-color-medium); }
    ion-footer ion-toolbar { padding: 8px; }
  `,
})
export class CarDetailPage implements OnInit {
  /** Bound from the :id route param via withComponentInputBinding(). */
  readonly id = input.required<string>();

  protected readonly store = inject(CarsStore);
  private readonly router = inject(Router);

  protected readonly car = this.store.selected;

  protected readonly canBook = computed(() => this.car()?.status === 'Active');

  ngOnInit(): void {
    void this.store.loadCar(this.id());
    void this.store.loadAvailability(this.id());
  }

  protected label(value: string): string {
    return humanize(value);
  }

  protected book(): void {
    void this.router.navigate(['/cars', this.id(), 'book']);
  }
}
