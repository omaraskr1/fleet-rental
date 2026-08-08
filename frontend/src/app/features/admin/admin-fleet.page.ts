import { Component, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { Router } from '@angular/router';
import { IonBadge, IonButton, IonNote, IonSpinner } from '@ionic/angular/standalone';

import { CarsStore } from '../../core/stores/cars.store';
import { LocaleStore } from '../../core/stores/locale.store';

/**
 * Fleet overview, plus add/edit/retire. Create/edit/retire route to their own
 * pages (`admin-car-form.page.ts`) rather than an inline form here, matching
 * how maintenance already gets its own page per car.
 */
@Component({
  selector: 'app-admin-fleet',
  imports: [IonBadge, IonButton, IonSpinner, IonNote, CurrencyPipe],
  template: `
    <div class="header">
      <div>
        <h1>{{ locale.t('admin.fleet.title') }}</h1>
        <ion-note>{{ locale.t('admin.fleet.freeToday', { free: store.availableCount(), total: store.cars().length }) }}</ion-note>
      </div>
      <ion-button size="small" (click)="addVehicle()">{{ locale.t('admin.fleet.addVehicle') }}</ion-button>
    </div>

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
              <th>{{ locale.t('admin.fleet.rate') }}</th>
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
                <td>
                  {{ car.rate | currency: 'USD' : 'symbol' : '1.0-0' }}{{ priceSuffix(car.pricingModel) }}
                </td>
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
                <td class="actions">
                  @if (retiringId() === car.id) {
                    <span class="confirm">
                      {{ locale.t('admin.fleet.retireConfirm') }}
                      <ion-button size="small" color="danger" (click)="confirmRetire(car.id)">
                        {{ locale.t('admin.fleet.retireConfirmButton') }}
                      </ion-button>
                      <ion-button size="small" fill="clear" (click)="retiringId.set(null)">
                        {{ locale.t('common.cancel') }}
                      </ion-button>
                    </span>
                  } @else {
                    <ion-button size="small" fill="clear" (click)="editVehicle(car.id)">
                      {{ locale.t('admin.fleet.edit') }}
                    </ion-button>
                    <ion-button size="small" fill="clear" (click)="openMaintenance(car.id)">
                      {{ locale.t('admin.fleet.maintenanceLink') }}
                    </ion-button>
                    @if (car.status !== 'Retired') {
                      <ion-button size="small" fill="clear" color="danger" (click)="retiringId.set(car.id)">
                        {{ locale.t('admin.fleet.retire') }}
                      </ion-button>
                    }
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    @if (store.error(); as error) {
      <ion-note color="danger" class="banner">{{ error }}</ion-note>
    }
  `,
  styles: `
    .header { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; }
    h1 { font-size: 1.4rem; margin: 0 0 4px; }
    .scroller { overflow-x: auto; margin-top: 20px; }
    table { border-collapse: collapse; width: 100%; font-size: 0.9rem; }
    th { text-align: start; font-weight: 500; color: var(--ion-color-medium);
         font-size: 0.75rem; text-transform: uppercase; padding-block: 8px; padding-inline-end: 12px; }
    td { padding-block: 10px; padding-inline-end: 12px;
         border-top: 1px solid var(--ion-color-light-shade); white-space: nowrap; }
    td.name { font-weight: 500; }
    td.actions { display: flex; gap: 4px; align-items: center; }
    .confirm { display: flex; align-items: center; gap: 8px; font-size: 0.85rem; color: var(--ion-color-medium); }
    .state { padding: 48px; text-align: center; }
    .banner { display: block; padding: 8px 0; white-space: pre-line; }
  `,
})
export class AdminFleetPage implements OnInit {
  protected readonly store = inject(CarsStore);
  protected readonly locale = inject(LocaleStore);
  private readonly router = inject(Router);

  protected readonly retiringId = signal<string | null>(null);

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

  protected priceSuffix(pricingModel: string): string {
    return pricingModel === 'PerEvent' ? this.locale.t('cars.perEvent') : this.locale.t('cars.perDay');
  }

  protected addVehicle(): void {
    void this.router.navigate(['/admin/fleet/new']);
  }

  protected editVehicle(carId: string): void {
    void this.router.navigate(['/admin/fleet', carId, 'edit']);
  }

  protected openMaintenance(carId: string): void {
    void this.router.navigate(['/admin/fleet', carId, 'maintenance']);
  }

  protected async confirmRetire(carId: string): Promise<void> {
    await this.store.retireCar(carId);
    this.retiringId.set(null);
  }
}
