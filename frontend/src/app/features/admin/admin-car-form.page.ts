import { Component, inject, input, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  IonButton, IonCard, IonCardContent, IonInput, IonItem, IonLabel, IonNote,
  IonSelect, IonSelectOption, IonSpinner, IonTextarea,
} from '@ionic/angular/standalone';

import { CarsStore } from '../../core/stores/cars.store';
import { FeaturesStore } from '../../core/stores/features.store';
import { LocaleStore } from '../../core/stores/locale.store';
import { ApiService } from '../../core/services/api.service';
import { CAR_CATEGORIES, PRICING_MODELS, type CarCategory, type CarStatus, type PricingModel } from '../../core/models';

/**
 * Add/edit vehicle. One component for both: `carId` present means edit (the
 * form loads and pre-fills the existing car), absent means add. The API
 * endpoints (POST/PUT /api/cars) and the CarsStore wrappers around them
 * already existed — only this UI was missing.
 */
@Component({
  selector: 'app-admin-car-form',
  imports: [
    IonCard, IonCardContent, IonItem, IonLabel, IonInput, IonSelect,
    IonSelectOption, IonTextarea, IonButton, IonNote, IonSpinner, FormsModule,
  ],
  template: `
    <h1>{{ isEditMode() ? locale.t('admin.fleet.form.editTitle') : locale.t('admin.fleet.form.addTitle') }}</h1>

    <ion-card>
      <ion-card-content>
        <ion-item>
          <ion-label position="stacked">{{ locale.t('admin.fleet.form.name') }}</ion-label>
          <ion-input [(ngModel)]="name" />
        </ion-item>

        <ion-item>
          <ion-label position="stacked">{{ locale.t('admin.fleet.form.description') }}</ion-label>
          <ion-textarea [(ngModel)]="description" [autoGrow]="true" />
        </ion-item>

        <ion-item>
          <ion-label position="stacked">{{ locale.t('admin.fleet.category') }}</ion-label>
          <ion-select [(ngModel)]="category" interface="action-sheet">
            @for (c of categories; track c) {
              <ion-select-option [value]="c">{{ locale.t('enums.category.' + c) }}</ion-select-option>
            }
          </ion-select>
        </ion-item>

        <ion-item>
          <ion-label position="stacked">{{ locale.t('admin.fleet.seats') }}</ion-label>
          <ion-input type="number" [(ngModel)]="seats" />
        </ion-item>

        <ion-item>
          <ion-label position="stacked">{{ locale.t('admin.fleet.form.rate') }}</ion-label>
          <ion-input type="number" [(ngModel)]="rate" />
        </ion-item>

        <ion-item>
          <ion-label position="stacked">{{ locale.t('admin.fleet.form.pricingModel') }}</ion-label>
          <ion-select [(ngModel)]="pricingModel" interface="action-sheet">
            @for (p of pricingModels; track p) {
              <ion-select-option [value]="p">{{ locale.t('admin.fleet.form.pricingModelOption.' + p) }}</ion-select-option>
            }
          </ion-select>
        </ion-item>

        <ion-item>
          <ion-label position="stacked">{{ locale.t('admin.fleet.form.licensePlateOptional') }}</ion-label>
          <ion-input [(ngModel)]="licensePlate" />
        </ion-item>

        @if (isEditMode()) {
          <ion-item>
            <ion-label position="stacked">{{ locale.t('admin.fleet.status') }}</ion-label>
            <ion-select [(ngModel)]="status" interface="action-sheet">
              <ion-select-option value="Active">{{ locale.t('cars.active') }}</ion-select-option>
              <ion-select-option value="Maintenance">{{ locale.t('cars.maintenance') }}</ion-select-option>
              <ion-select-option value="Retired">{{ locale.t('cars.retired') }}</ion-select-option>
            </ion-select>
          </ion-item>
        }
      </ion-card-content>
    </ion-card>

    @if (isEditMode() && features.isEnabled('Gps')) {
      <ion-card>
        <ion-card-content>
          <h2>{{ locale.t('admin.fleet.form.gpsDeviceKey') }}</h2>
          <p class="muted">{{ locale.t('admin.fleet.form.gpsDeviceKeyHelp') }}</p>
          <div class="device-key">
            <code>{{ deviceKey() || locale.t('admin.fleet.form.gpsDeviceKeyNone') }}</code>
            <ion-button size="small" fill="outline" [disabled]="regenerating()" (click)="regenerateDeviceKey()">
              @if (regenerating()) {
                <ion-spinner name="dots" />
              } @else {
                {{ locale.t('admin.fleet.form.gpsDeviceKeyRegenerate') }}
              }
            </ion-button>
          </div>
        </ion-card-content>
      </ion-card>
    }

    @if (cars.error(); as error) {
      <ion-note color="danger" class="banner">{{ error }}</ion-note>
    }

    <ion-button expand="block" [disabled]="!isValid() || cars.loading()" (click)="submit()">
      @if (cars.loading()) {
        <ion-spinner name="dots" />
      } @else {
        {{ locale.t('admin.fleet.form.save') }}
      }
    </ion-button>
  `,
  styles: `
    h1 { font-size: 1.4rem; margin: 0 0 16px; }
    h2 { font-size: 1rem; margin: 0; }
    .muted { color: var(--ion-color-medium); font-size: 0.85rem; margin: 4px 0 12px; }
    .device-key { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
    .device-key code { font-size: 0.85rem; word-break: break-all; }
    .banner { display: block; padding: 8px 0; white-space: pre-line; }
    ion-button { margin-top: 16px; max-width: 320px; }
  `,
})
export class AdminCarFormPage implements OnInit {
  readonly carId = input<string>();

  protected readonly cars = inject(CarsStore);
  protected readonly features = inject(FeaturesStore);
  protected readonly locale = inject(LocaleStore);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  protected readonly deviceKey = signal<string | null>(null);
  protected readonly regenerating = signal(false);

  protected readonly categories = CAR_CATEGORIES;
  protected readonly pricingModels = PRICING_MODELS;

  protected name = '';
  protected description = '';
  protected category: CarCategory = 'Sedan';
  protected seats: number | null = null;
  protected rate: number | null = null;
  protected pricingModel: PricingModel = 'PerDay';
  protected licensePlate = '';
  protected status: CarStatus = 'Active';

  protected isEditMode(): boolean {
    return !!this.carId();
  }

  async ngOnInit(): Promise<void> {
    const id = this.carId();
    if (!id) {
      return;
    }

    await this.cars.loadCar(id);
    const car = this.cars.selected();
    if (!car) {
      return;
    }

    this.name = car.name;
    this.description = car.description;
    this.category = car.category;
    this.seats = car.seats;
    this.rate = car.rate;
    this.pricingModel = car.pricingModel;
    this.licensePlate = car.licensePlate ?? '';
    this.status = car.status;

    // Attempted unconditionally rather than gated on features.isEnabled('Gps')
    // — that signal may not have finished loading yet on a direct deep link to
    // this page, and the template's own reactive check already hides the
    // section if Gps turns out to be off, so a 403 here is harmless and simply
    // leaves the section empty.
    try {
      const key = await this.api.getGpsDeviceKey(id);
      this.deviceKey.set(key.deviceKey);
    } catch {
      // Non-critical to the form itself.
    }
  }

  protected async regenerateDeviceKey(): Promise<void> {
    const id = this.carId();
    if (!id) {
      return;
    }

    this.regenerating.set(true);
    try {
      const key = await this.api.regenerateGpsDeviceKey(id);
      this.deviceKey.set(key.deviceKey);
    } finally {
      this.regenerating.set(false);
    }
  }

  /** A plain method, not computed() — see BUG-001 in BUGS.md for why. */
  protected isValid(): boolean {
    return this.name.trim().length > 0 && this.seats !== null && this.seats > 0 && this.rate !== null && this.rate >= 0;
  }

  protected async submit(): Promise<void> {
    if (!this.isValid()) {
      return;
    }

    const request = {
      name: this.name.trim(),
      description: this.description.trim(),
      category: this.category,
      seats: this.seats!,
      rate: this.rate!,
      pricingModel: this.pricingModel,
      licensePlate: this.licensePlate.trim() || null,
    };

    try {
      const id = this.carId();
      if (id) {
        await this.cars.updateCar(id, { ...request, status: this.status });
      } else {
        await this.cars.createCar(request);
      }

      await this.router.navigate(['/admin/fleet']);
    } catch {
      // The store surfaced the message; keep the form open so nothing is lost.
    }
  }
}
