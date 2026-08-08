import { Component, computed, inject, input, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  IonButton, IonCard, IonCardContent, IonNote, IonSpinner, IonToggle,
} from '@ionic/angular/standalone';

import { PlatformCompaniesStore } from '../../core/stores/platform-companies.store';
import { PlatformFeaturesStore } from '../../core/stores/platform-features.store';
import { LocaleStore } from '../../core/stores/locale.store';
import { FEATURE_KEYS, type FeatureKey } from '../../core/models';

/** Turns individual features on or off for one company — reached from the Companies list. */
@Component({
  selector: 'app-platform-company-features',
  imports: [IonCard, IonCardContent, IonToggle, IonButton, IonNote, IonSpinner, FormsModule],
  template: `
    <ion-button fill="clear" size="small" (click)="back()">
      &larr; {{ locale.t('platform.companyAdmins.back') }}
    </ion-button>

    <h1>{{ locale.t('platform.companyFeatures.title', { company: companyName() }) }}</h1>

    @if (features.error(); as error) {
      <ion-note color="danger" class="banner">{{ error }}</ion-note>
    }

    @if (features.loading() && features.toggles().length === 0) {
      <div class="state"><ion-spinner /></div>
    } @else {
      @for (key of featureKeys; track key) {
        <ion-card>
          <ion-card-content>
            <div class="row">
              <div>
                <h2>{{ locale.t('platform.companyFeatures.feature.' + key + '.name') }}</h2>
                <p class="muted">{{ locale.t('platform.companyFeatures.feature.' + key + '.description') }}</p>
              </div>
              <ion-toggle
                [checked]="isEnabled(key)"
                (ionChange)="toggle(key, $event.detail.checked)"
              ></ion-toggle>
            </div>
          </ion-card-content>
        </ion-card>
      }
    }
  `,
  styles: `
    h1 { font-size: 1.4rem; margin: 8px 0 16px; }
    h2 { font-size: 1rem; margin: 0; }
    .row { display: flex; justify-content: space-between; align-items: center; gap: 16px; }
    .muted { color: var(--ion-color-medium); font-size: 0.85rem; margin: 4px 0 0; max-width: 46ch; }
    .state { padding: 48px; text-align: center; }
    .banner { display: block; padding: 8px 0; white-space: pre-line; }
  `,
})
export class PlatformCompanyFeaturesPage implements OnInit {
  readonly tenantId = input.required<string>();

  protected readonly companies = inject(PlatformCompaniesStore);
  protected readonly features = inject(PlatformFeaturesStore);
  protected readonly locale = inject(LocaleStore);
  private readonly router = inject(Router);

  protected readonly featureKeys = FEATURE_KEYS;

  protected readonly companyName = computed(
    () => this.companies.companies().find((c) => c.id === this.tenantId())?.name ?? '',
  );

  ngOnInit(): void {
    if (this.companies.companies().length === 0) {
      void this.companies.loadCompanies();
    }

    void this.features.load(this.tenantId());
  }

  protected isEnabled(key: FeatureKey): boolean {
    return this.features.toggles().find((t) => t.key === key)?.isEnabled ?? true;
  }

  protected async toggle(key: FeatureKey, isEnabled: boolean): Promise<void> {
    await this.features.setEnabled(this.tenantId(), key, isEnabled);
  }

  protected back(): void {
    void this.router.navigate(['/platform/companies']);
  }
}
