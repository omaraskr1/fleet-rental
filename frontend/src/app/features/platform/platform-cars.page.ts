import { Component, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  IonBadge, IonButton, IonCard, IonCardContent, IonInput, IonItem, IonLabel,
  IonNote, IonSelect, IonSelectOption, IonSpinner,
} from '@ionic/angular/standalone';

import { PlatformCarsStore } from '../../core/stores/platform-cars.store';
import { PlatformCompaniesStore } from '../../core/stores/platform-companies.store';
import { LocaleStore } from '../../core/stores/locale.store';
import { CAR_CATEGORIES, PRICING_MODELS, type CarCategory, type PlatformCar, type PricingModel } from '../../core/models';

/**
 * Every vehicle across every company, in one screen — add, edit, retire, and see
 * which company owns what. Follows the same add-inline-form-plus-table pattern
 * as AdminFleetPage, with a company column and selector since a car here can
 * belong to any tenant.
 */
@Component({
  selector: 'app-platform-cars',
  imports: [
    IonCard, IonCardContent, IonItem, IonLabel, IonInput, IonSelect, IonSelectOption,
    IonButton, IonBadge, IonNote, IonSpinner, FormsModule, CurrencyPipe,
  ],
  template: `
    <h1>{{ locale.t('platform.cars.title') }}</h1>

    <ion-card>
      <ion-card-content>
        <h2>{{ locale.t('platform.cars.form.addTitle') }}</h2>
        <div class="inline-form">
          <ion-item>
            <ion-label position="stacked">{{ locale.t('platform.cars.form.company') }}</ion-label>
            <ion-select [(ngModel)]="newCompanyId" interface="action-sheet">
              @for (company of companies.companies(); track company.id) {
                <ion-select-option [value]="company.id">{{ company.name }}</ion-select-option>
              }
            </ion-select>
          </ion-item>
          <ion-item>
            <ion-label position="stacked">{{ locale.t('platform.cars.form.name') }}</ion-label>
            <ion-input [(ngModel)]="newName" />
          </ion-item>
          <ion-item>
            <ion-label position="stacked">{{ locale.t('platform.cars.form.category') }}</ion-label>
            <ion-select [(ngModel)]="newCategory" interface="action-sheet">
              @for (c of categories; track c) {
                <ion-select-option [value]="c">{{ locale.t('enums.category.' + c) }}</ion-select-option>
              }
            </ion-select>
          </ion-item>
          <ion-item>
            <ion-label position="stacked">{{ locale.t('platform.cars.form.seats') }}</ion-label>
            <ion-input type="number" [(ngModel)]="newSeats" />
          </ion-item>
          <ion-item>
            <ion-label position="stacked">{{ locale.t('platform.cars.form.rate') }}</ion-label>
            <ion-input type="number" [(ngModel)]="newRate" />
          </ion-item>
          <ion-item>
            <ion-label position="stacked">{{ locale.t('platform.cars.form.pricingModel') }}</ion-label>
            <ion-select [(ngModel)]="newPricingModel" interface="action-sheet">
              @for (p of pricingModels; track p) {
                <ion-select-option [value]="p">{{ locale.t('admin.fleet.form.pricingModelOption.' + p) }}</ion-select-option>
              }
            </ion-select>
          </ion-item>
          <ion-button [disabled]="!isValid() || cars.submitting()" (click)="submit()">
            @if (cars.submitting()) { <ion-spinner name="dots" /> } @else { {{ locale.t('platform.cars.addCar') }} }
          </ion-button>
        </div>
      </ion-card-content>
    </ion-card>

    @if (cars.error(); as error) {
      <ion-note color="danger" class="banner">{{ error }}</ion-note>
    }

    @if (cars.loading() && cars.cars().length === 0) {
      <div class="state"><ion-spinner /></div>
    } @else {
      <div class="scroller">
        <table>
          <thead>
            <tr>
              <th>{{ locale.t('platform.cars.company') }}</th>
              <th>{{ locale.t('platform.cars.vehicle') }}</th>
              <th>{{ locale.t('platform.cars.category') }}</th>
              <th>{{ locale.t('platform.cars.seats') }}</th>
              <th>{{ locale.t('platform.cars.rate') }}</th>
              <th>{{ locale.t('platform.cars.status') }}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (car of cars.cars(); track car.id) {
              @if (editingId() === car.id) {
                <tr class="editing">
                  <td>{{ car.companyName }}</td>
                  <td><ion-input [(ngModel)]="editName" /></td>
                  <td>
                    <ion-select [(ngModel)]="editCategory" interface="action-sheet">
                      @for (c of categories; track c) {
                        <ion-select-option [value]="c">{{ locale.t('enums.category.' + c) }}</ion-select-option>
                      }
                    </ion-select>
                  </td>
                  <td><ion-input type="number" [(ngModel)]="editSeats" /></td>
                  <td><ion-input type="number" [(ngModel)]="editRate" /></td>
                  <td>{{ car.status }}</td>
                  <td class="actions">
                    <ion-button size="small" (click)="saveEdit(car)">{{ locale.t('common.save') }}</ion-button>
                    <ion-button size="small" fill="clear" (click)="editingId.set(null)">{{ locale.t('common.cancel') }}</ion-button>
                  </td>
                </tr>
              } @else {
                <tr>
                  <td>{{ car.companyName }}</td>
                  <td class="name">{{ car.name }}</td>
                  <td>{{ locale.t('enums.category.' + car.category) }}</td>
                  <td>{{ car.seats }}</td>
                  <td>
                    {{ car.rate | currency: 'USD' : 'symbol' : '1.0-0' }}{{ car.pricingModel === 'PerEvent' ? locale.t('cars.perEvent') : locale.t('cars.perDay') }}
                  </td>
                  <td>
                    <ion-badge [color]="car.status === 'Active' ? 'success' : 'medium'">{{ car.status }}</ion-badge>
                  </td>
                  <td class="actions">
                    @if (retiringId() === car.id) {
                      <span class="confirm">
                        {{ locale.t('platform.cars.retireConfirm') }}
                        <ion-button size="small" color="danger" (click)="confirmRetire(car.id)">
                          {{ locale.t('platform.cars.retireConfirmButton') }}
                        </ion-button>
                        <ion-button size="small" fill="clear" (click)="retiringId.set(null)">
                          {{ locale.t('common.cancel') }}
                        </ion-button>
                      </span>
                    } @else {
                      <ion-button size="small" fill="clear" (click)="startEdit(car)">
                        {{ locale.t('platform.cars.edit') }}
                      </ion-button>
                      @if (car.status !== 'Retired') {
                        <ion-button size="small" fill="clear" color="danger" (click)="retiringId.set(car.id)">
                          {{ locale.t('platform.cars.retire') }}
                        </ion-button>
                      }
                    }
                  </td>
                </tr>
              }
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: `
    h1 { font-size: 1.4rem; margin: 0 0 16px; }
    h2 { font-size: 1rem; margin: 0; }
    .inline-form { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px; align-items: end; }
    .scroller { overflow-x: auto; margin-top: 20px; }
    table { border-collapse: collapse; width: 100%; font-size: 0.9rem; }
    th { text-align: start; font-weight: 500; color: var(--ion-color-medium);
         font-size: 0.75rem; text-transform: uppercase; padding-block: 8px; padding-inline-end: 12px; }
    td { padding-block: 10px; padding-inline-end: 12px;
         border-top: 1px solid var(--ion-color-light-shade); white-space: nowrap; }
    td.name { font-weight: 500; }
    td.actions { display: flex; gap: 4px; align-items: center; }
    tr.editing td { padding-block: 6px; }
    .confirm { display: flex; align-items: center; gap: 8px; font-size: 0.85rem; color: var(--ion-color-medium); }
    .state { padding: 48px; text-align: center; }
    .banner { display: block; padding: 8px 0; white-space: pre-line; }
  `,
})
export class PlatformCarsPage implements OnInit {
  protected readonly cars = inject(PlatformCarsStore);
  protected readonly companies = inject(PlatformCompaniesStore);
  protected readonly locale = inject(LocaleStore);

  protected readonly categories = CAR_CATEGORIES;
  protected readonly pricingModels = PRICING_MODELS;

  protected newCompanyId = '';
  protected newName = '';
  protected newCategory: CarCategory = 'Sedan';
  protected newSeats: number | null = null;
  protected newRate: number | null = null;
  protected newPricingModel: PricingModel = 'PerDay';

  protected readonly retiringId = signal<string | null>(null);
  protected readonly editingId = signal<string | null>(null);
  protected editName = '';
  protected editCategory: CarCategory = 'Sedan';
  protected editSeats: number | null = null;
  protected editRate: number | null = null;

  ngOnInit(): void {
    void this.cars.loadCars();
    if (this.companies.companies().length === 0) {
      void this.companies.loadCompanies();
    }
  }

  /** A plain method, not computed() — see BUG-001 in BUGS.md for why. */
  protected isValid(): boolean {
    return (
      this.newCompanyId.trim().length > 0 &&
      this.newName.trim().length > 0 &&
      this.newSeats !== null &&
      this.newSeats > 0 &&
      this.newRate !== null &&
      this.newRate > 0
    );
  }

  protected async submit(): Promise<void> {
    if (!this.isValid()) {
      return;
    }

    await this.cars.createCar({
      companyId: this.newCompanyId,
      name: this.newName.trim(),
      description: '',
      category: this.newCategory,
      seats: this.newSeats!,
      rate: this.newRate!,
      pricingModel: this.newPricingModel,
    });

    this.newCompanyId = '';
    this.newName = '';
    this.newCategory = 'Sedan';
    this.newSeats = null;
    this.newRate = null;
    this.newPricingModel = 'PerDay';
  }

  protected startEdit(car: PlatformCar): void {
    this.retiringId.set(null);
    this.editingId.set(car.id);
    this.editName = car.name;
    this.editCategory = car.category;
    this.editSeats = car.seats;
    this.editRate = car.rate;
  }

  protected async saveEdit(car: PlatformCar): Promise<void> {
    await this.cars.updateCar(car.id, {
      name: this.editName.trim(),
      description: car.description,
      category: this.editCategory,
      seats: this.editSeats!,
      rate: this.editRate!,
      pricingModel: car.pricingModel,
      licensePlate: car.licensePlate,
    });

    this.editingId.set(null);
  }

  protected async confirmRetire(carId: string): Promise<void> {
    await this.cars.retireCar(carId);
    this.retiringId.set(null);
  }
}
