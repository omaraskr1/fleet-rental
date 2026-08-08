import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { IonBadge, IonButton, IonCard, IonCardContent, IonInput, IonItem, IonLabel, IonNote, IonSpinner } from '@ionic/angular/standalone';

import { MaintenanceStore } from '../../core/stores/maintenance.store';
import { LocaleStore } from '../../core/stores/locale.store';

/**
 * Fleet-wide service catalog — "Oil change every 10,000 km" defined once,
 * shared across every car. The per-car km-until-due breakdown lives on each
 * car's maintenance page; this is where the catalog itself is managed.
 */
@Component({
  selector: 'app-admin-service-catalog',
  imports: [IonCard, IonCardContent, IonItem, IonLabel, IonInput, IonButton, IonBadge, IonNote, IonSpinner, FormsModule],
  template: `
    <h1>{{ locale.t('admin.services.title') }}</h1>

    <ion-card>
      <ion-card-content>
        <h2>{{ locale.t('admin.services.add') }}</h2>
        <div class="inline-form">
          <ion-item>
            <ion-label position="stacked">{{ locale.t('admin.services.name') }}</ion-label>
            <ion-input [(ngModel)]="newName" [placeholder]="locale.t('admin.services.namePlaceholder')" />
          </ion-item>
          <ion-item>
            <ion-label position="stacked">{{ locale.t('admin.services.intervalKm') }}</ion-label>
            <ion-input type="number" [(ngModel)]="newIntervalKm" />
          </ion-item>
          <ion-button [disabled]="!isValid() || store.submitting()" (click)="submit()">
            @if (store.submitting()) { <ion-spinner name="dots" /> } @else { {{ locale.t('admin.services.add') }} }
          </ion-button>
        </div>
      </ion-card-content>
    </ion-card>

    @if (store.error(); as error) {
      <ion-note color="danger" class="banner">{{ error }}</ion-note>
    }

    @if (store.loading() && store.serviceTypes().length === 0) {
      <div class="state"><ion-spinner /></div>
    } @else if (store.serviceTypes().length === 0) {
      <p class="state">{{ locale.t('admin.services.empty') }}</p>
    } @else {
      @for (type of store.serviceTypes(); track type.id) {
        <ion-card>
          <ion-card-content>
            <div class="row">
              <div>
                <h2>{{ type.name }}</h2>
                <p class="muted">{{ locale.t('admin.services.every', { km: type.intervalKm }) }}</p>
              </div>
              <div class="actions">
                <ion-badge [color]="type.isActive ? 'success' : 'medium'">
                  {{ type.isActive ? locale.t('admin.services.active') : locale.t('admin.services.inactive') }}
                </ion-badge>
                @if (type.isActive) {
                  <ion-button size="small" fill="clear" color="danger" (click)="deactivate(type.id)">
                    {{ locale.t('admin.services.deactivate') }}
                  </ion-button>
                } @else {
                  <ion-button size="small" fill="clear" (click)="reactivate(type.id)">
                    {{ locale.t('admin.services.reactivate') }}
                  </ion-button>
                }
              </div>
            </div>
          </ion-card-content>
        </ion-card>
      }
    }
  `,
  styles: `
    h1 { font-size: 1.4rem; margin: 0 0 16px; }
    h2 { font-size: 1rem; margin: 0; }
    .inline-form { display: grid; grid-template-columns: 1fr auto auto; gap: 8px; align-items: end; }
    .row { display: flex; justify-content: space-between; align-items: center; gap: 12px; }
    .actions { display: flex; align-items: center; gap: 8px; }
    .muted { color: var(--ion-color-medium); font-size: 0.85rem; margin: 4px 0 0; }
    .banner { display: block; padding: 12px 0; white-space: pre-line; }
    .state { padding: 48px; text-align: center; color: var(--ion-color-medium); }
  `,
})
export class AdminServiceCatalogPage implements OnInit {
  protected readonly store = inject(MaintenanceStore);
  protected readonly locale = inject(LocaleStore);

  protected newName = '';
  protected newIntervalKm: number | null = null;

  ngOnInit(): void {
    void this.store.loadServiceTypes(true);
  }

  /** A plain method, not computed() — see BUG-001 in BUGS.md for why. */
  protected isValid(): boolean {
    return this.newName.trim().length > 0 && this.newIntervalKm !== null && this.newIntervalKm > 0;
  }

  protected async submit(): Promise<void> {
    if (!this.isValid()) {
      return;
    }

    await this.store.createServiceType({ name: this.newName.trim(), intervalKm: this.newIntervalKm! });
    this.newName = '';
    this.newIntervalKm = null;
  }

  protected async deactivate(id: string): Promise<void> {
    await this.store.deactivateServiceType(id);
  }

  protected async reactivate(id: string): Promise<void> {
    await this.store.reactivateServiceType(id);
  }
}
