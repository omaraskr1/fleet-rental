import { Component, inject, OnInit } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { Router } from '@angular/router';
import { IonBadge, IonButton, IonNote, IonSpinner } from '@ionic/angular/standalone';

import { CarsStore } from '../../core/stores/cars.store';
import { LocaleStore } from '../../core/stores/locale.store';

/**
 * Read-only fleet overview. Vehicle create/edit is deliberately out of Phase 1
 * scope — the API endpoints exist (POST/PUT /api/cars) and are admin-guarded,
 * so the management UI is additive rather than a rewrite.
 */
@Component({
  selector: 'app-admin-fleet',
  imports: [IonBadge, IonButton, IonSpinner, IonNote, CurrencyPipe],
  template: `
    <h1>{{ locale.t('admin.fleet.title') }}</h1>
    <ion-note>{{ locale.t('admin.fleet.freeToday', { free: store.availableCount(), total: store.cars().length }) }}</ion-note>

    @if (store.loading() && store.cars().length === 0) {
      <div class="state"><ion-spinner /></div>
    } @else {
      <div class="scroller">
        <table>
          <thead>
            <tr>
              <th>{{ locale.t('admin.fleet.vehicle') }}</th>
              <th>{{ locale.t('admin.fleet.category') }}</th>
              <th>{{ locale.t('admin.fleet.seats') }}</th>
              <th>{{ locale.t('admin.fleet.dailyRate') }}</th>
              <th>{{ locale.t('admin.fleet.status') }}</th>
              <th>{{ locale.t('admin.fleet.today') }}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (car of store.cars(); track car.id) {
              <tr>
                <td class="name">{{ car.name }}</td>
                <td>{{ categoryLabel(car.category) }}</td>
                <td>{{ car.seats }}</td>
                <td>{{ car.dailyRate | currency: 'USD' : 'symbol' : '1.0-0' }}</td>
                <td>
                  <ion-badge [color]="car.status === 'Active' ? 'success' : 'medium'">
                    {{ statusLabel(car.status) }}
                  </ion-badge>
                </td>
                <td>
                  <ion-badge [color]="car.availableToday ? 'success' : 'warning'">
                    {{ car.availableToday ? locale.t('admin.fleet.free') : locale.t('admin.fleet.booked') }}
                  </ion-badge>
                </td>
                <td>
                  <ion-button size="small" fill="clear" (click)="openMaintenance(car.id)">
                    {{ locale.t('admin.fleet.maintenanceLink') }}
                  </ion-button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: `
    h1 { font-size: 1.4rem; margin: 0 0 4px; }
    .scroller { overflow-x: auto; margin-top: 20px; }
    table { border-collapse: collapse; width: 100%; font-size: 0.9rem; }
    th { text-align: start; font-weight: 500; color: var(--ion-color-medium);
         font-size: 0.75rem; text-transform: uppercase; padding-block: 8px; padding-inline-end: 12px; }
    td { padding-block: 10px; padding-inline-end: 12px;
         border-top: 1px solid var(--ion-color-light-shade); white-space: nowrap; }
    td.name { font-weight: 500; }
    .state { padding: 48px; text-align: center; }
  `,
})
export class AdminFleetPage implements OnInit {
  protected readonly store = inject(CarsStore);
  protected readonly locale = inject(LocaleStore);
  private readonly router = inject(Router);

  ngOnInit(): void {
    void this.store.loadCars();
  }

  protected categoryLabel(value: string): string {
    return this.locale.t(`enums.category.${value}`);
  }

  protected statusLabel(value: string): string {
    switch (value) {
      case 'Active':
        return this.locale.t('cars.active');
      case 'Maintenance':
        return this.locale.t('cars.maintenance');
      case 'Retired':
        return this.locale.t('cars.retired');
      default:
        return value;
    }
  }

  protected openMaintenance(carId: string): void {
    void this.router.navigate(['/admin/fleet', carId, 'maintenance']);
  }
}
