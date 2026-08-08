import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  IonButton, IonContent, IonInput, IonItem, IonLabel, IonNote, IonSpinner,
} from '@ionic/angular/standalone';

import { TenantStore } from '../../core/stores/tenant.store';
import { LocaleStore } from '../../core/stores/locale.store';

/**
 * First screen on a fresh install: which rental company is this for?
 *
 * One app in the store serves every tenant on the platform, so this is the
 * equivalent of picking a workspace in Slack — a one-time step whose answer is
 * then remembered on the device.
 */
@Component({
  selector: 'app-select-company',
  imports: [IonContent, IonItem, IonLabel, IonInput, IonButton, IonNote, IonSpinner, FormsModule],
  template: `
    <ion-content class="ion-padding">
      <div class="panel">
        <h1>{{ locale.t('tenant.title') }}</h1>
        <p class="sub">{{ locale.t('tenant.subtitle') }}</p>

        <ion-item>
          <ion-label position="stacked">{{ locale.t('tenant.codeLabel') }}</ion-label>
          <ion-input
            [(ngModel)]="code"
            [placeholder]="locale.t('tenant.codePlaceholder')"
            autocapitalize="off"
            autocorrect="off"
            (keyup.enter)="submit()" />
          <ion-note slot="helper">{{ locale.t('tenant.helper') }}</ion-note>
        </ion-item>

        @if (tenant.error(); as error) {
          <ion-note color="danger" class="error">{{ error }}</ion-note>
        }

        <ion-button expand="block" [disabled]="!code.trim() || tenant.loading()" (click)="submit()">
          @if (tenant.loading()) { <ion-spinner name="dots" /> } @else { {{ locale.t('tenant.continueButton') }} }
        </ion-button>
      </div>
    </ion-content>
  `,
  styles: `
    .panel { max-width: 400px; margin: 15vh auto 0; text-align: center; }
    h1 { font-size: 1.6rem; margin-bottom: 0; }
    .sub { color: var(--ion-color-medium); margin-top: 4px; }
    ion-item { text-align: start; }
    ion-button { margin-top: 24px; }
    .error { display: block; margin-top: 16px; }
  `,
})
export class SelectCompanyPage {
  protected readonly tenant = inject(TenantStore);
  protected readonly locale = inject(LocaleStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected code = '';

  protected async submit(): Promise<void> {
    const found = await this.tenant.selectByCode(this.code);

    if (found) {
      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/tabs/cars';
      await this.router.navigateByUrl(returnUrl, { replaceUrl: true });
    }
  }
}
