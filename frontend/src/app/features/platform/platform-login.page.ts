import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  IonButton, IonContent, IonInput, IonItem, IonLabel, IonNote, IonSpinner,
} from '@ionic/angular/standalone';

import { PlatformAuthStore } from '../../core/stores/platform-auth.store';
import { LocaleStore } from '../../core/stores/locale.store';
import { PreferencesToggleComponent } from '../../shared/preferences-toggle.component';

/** Login for the platform panel — entirely separate identity from any tenant's admin. */
@Component({
  selector: 'app-platform-login',
  imports: [
    IonContent, IonItem, IonLabel, IonInput, IonButton, IonNote, IonSpinner,
    FormsModule, PreferencesToggleComponent,
  ],
  template: `
    <ion-content class="ion-padding">
      <div class="panel">
        <div class="prefs"><app-preferences-toggle /></div>

        <h1>{{ locale.t('common.appName') }}</h1>
        <p class="sub">{{ locale.t('platform.login.title') }}</p>
        <p class="sub2">{{ locale.t('platform.login.subtitle') }}</p>

        <ion-item>
          <ion-label position="stacked">{{ locale.t('platform.login.email') }}</ion-label>
          <ion-input type="email" [(ngModel)]="email" inputmode="email"
                     autocomplete="email" (keyup.enter)="submit()" />
        </ion-item>

        <ion-item>
          <ion-label position="stacked">{{ locale.t('platform.login.password') }}</ion-label>
          <ion-input type="password" [(ngModel)]="password"
                     autocomplete="current-password" (keyup.enter)="submit()" />
        </ion-item>

        @if (auth.error(); as error) {
          <ion-note color="danger" class="error">{{ error }}</ion-note>
        }

        <ion-button expand="block" [disabled]="auth.loading()" (click)="submit()">
          @if (auth.loading()) { <ion-spinner name="dots" /> } @else { {{ locale.t('platform.login.signInButton') }} }
        </ion-button>
      </div>
    </ion-content>
  `,
  styles: `
    .panel { max-width: 400px; margin: 6vh auto 0; }
    .prefs { display: flex; justify-content: center; margin-bottom: 24px; }
    h1 { font-size: 1.6rem; margin-bottom: 0; text-align: center; }
    .sub { color: var(--ion-color-medium); margin-top: 4px; text-align: center; }
    .sub2 { color: var(--ion-color-medium); margin-top: -8px; text-align: center; font-size: 0.85rem; }
    ion-button { margin-top: 24px; }
    .error { display: block; margin-top: 16px; white-space: pre-line; }
  `,
})
export class PlatformLoginPage {
  protected readonly auth = inject(PlatformAuthStore);
  protected readonly locale = inject(LocaleStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected email = '';
  protected password = '';

  protected async submit(): Promise<void> {
    if (!this.email || !this.password) return;

    try {
      await this.auth.login(this.email, this.password);
      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/platform';
      await this.router.navigateByUrl(returnUrl, { replaceUrl: true });
    } catch {
      // Message already on auth.error().
    }
  }
}
