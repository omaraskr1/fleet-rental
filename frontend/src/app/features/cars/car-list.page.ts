import { Component, inject, OnInit } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { Router } from '@angular/router';
import {
  IonBadge,
  IonCard,
  IonCardContent,
  IonChip,
  IonContent,
  IonHeader,
  IonIcon,
  IonImg,
  IonLabel,
  IonRefresher,
  IonRefresherContent,
  IonSkeletonText,
  IonSpinner,
  IonTitle,
  IonToolbar,
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { carSportOutline, peopleOutline } from 'ionicons/icons';

import { CarsStore } from '../../core/stores/cars.store';
import { LocaleStore } from '../../core/stores/locale.store';
import { CAR_CATEGORIES, type CarCategory } from '../../core/models';

/** Feature 1 — the fleet browse screen. */
@Component({
  selector: 'app-car-list',
  imports: [
    IonHeader, IonToolbar, IonTitle, IonContent, IonCard, IonCardContent, IonImg,
    IonBadge, IonChip, IonLabel, IonIcon, IonRefresher, IonRefresherContent,
    IonSkeletonText, IonSpinner, CurrencyPipe,
  ],
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-title>{{ locale.t('cars.title') }}</ion-title>
      </ion-toolbar>
      <ion-toolbar>
        <div class="filters">
          <ion-chip
            [outline]="store.categoryFilter() !== null"
            (click)="filter(null)">
            <ion-label>{{ locale.t('cars.filterAll') }}</ion-label>
          </ion-chip>
          @for (category of categories; track category) {
            <ion-chip
              [outline]="store.categoryFilter() !== category"
              (click)="filter(category)">
              <ion-label>{{ label(category) }}</ion-label>
            </ion-chip>
          }
        </div>
      </ion-toolbar>
    </ion-header>

    <ion-content>
      <ion-refresher slot="fixed" (ionRefresh)="refresh($event)">
        <ion-refresher-content />
      </ion-refresher>

      @if (store.loading() && store.cars().length === 0) {
        @for (i of skeletons; track i) {
          <ion-card>
            <ion-skeleton-text [animated]="true" style="height: 180px" />
            <ion-card-content>
              <ion-skeleton-text [animated]="true" style="width: 60%" />
              <ion-skeleton-text [animated]="true" style="width: 40%" />
            </ion-card-content>
          </ion-card>
        }
      } @else if (store.error()) {
        <div class="state">
          <p>{{ store.error() }}</p>
        </div>
      } @else if (store.isEmpty()) {
        <div class="state">
          <ion-icon name="car-sport-outline" />
          <p>{{ locale.t('cars.noResults') }}</p>
        </div>
      } @else {
        @for (car of store.visibleCars(); track car.id) {
          <ion-card button (click)="open(car.id)">
            @if (car.primaryPhotoUrl) {
              <ion-img [src]="car.primaryPhotoUrl" [alt]="car.name" />
            }
            <ion-card-content>
              <div class="row">
                <h2>{{ car.name }}</h2>
                <ion-badge [color]="car.availableToday ? 'success' : 'medium'">
                  {{ car.availableToday ? locale.t('cars.available') : locale.t('cars.bookedToday') }}
                </ion-badge>
              </div>
              <div class="meta">
                <span>{{ label(car.category) }}</span>
                <span>·</span>
                <ion-icon name="people-outline" />
                <span>{{ car.seats }}</span>
                <span>·</span>
                <strong>{{ car.dailyRate | currency: 'USD' : 'symbol' : '1.0-0' }}{{ locale.t('cars.perDay') }}</strong>
              </div>
            </ion-card-content>
          </ion-card>
        }
      }

      @if (store.loading() && store.cars().length > 0) {
        <div class="state"><ion-spinner /></div>
      }
    </ion-content>
  `,
  styles: `
    .filters { display: flex; gap: 4px; overflow-x: auto; padding: 4px 12px; }
    .filters ion-chip { flex: 0 0 auto; }
    ion-img { height: 180px; object-fit: cover; }
    .row { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
    h2 { margin: 0; font-size: 1.1rem; font-weight: 600; }
    .meta { display: flex; align-items: center; gap: 6px; margin-top: 6px;
            color: var(--ion-color-medium); font-size: 0.9rem; }
    .state { display: flex; flex-direction: column; align-items: center;
             gap: 8px; padding: 48px 24px; color: var(--ion-color-medium); }
    .state ion-icon { font-size: 48px; }
  `,
})
export class CarListPage implements OnInit {
  protected readonly store = inject(CarsStore);
  protected readonly locale = inject(LocaleStore);
  private readonly router = inject(Router);

  protected readonly categories = CAR_CATEGORIES;
  protected readonly skeletons = [1, 2, 3];

  constructor() {
    addIcons({ carSportOutline, peopleOutline });
  }

  ngOnInit(): void {
    void this.store.loadCars();
  }

  protected label(value: string): string {
    return this.locale.t(`enums.category.${value}`);
  }

  protected filter(category: CarCategory | null): void {
    this.store.setCategoryFilter(category);
  }

  protected open(id: string): void {
    void this.router.navigate(['/cars', id]);
  }

  protected async refresh(event: CustomEvent): Promise<void> {
    await this.store.loadCars();
    (event.target as HTMLIonRefresherElement).complete();
  }
}
