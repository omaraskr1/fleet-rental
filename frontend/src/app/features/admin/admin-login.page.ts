import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  IonButton, IonContent, IonInput, IonItem, IonLabel, IonNote, IonSpinner,
} from '@ionic/angular/standalone';

import { AuthStore } from '../../core/stores/auth.store';
import { LocaleStore } from '../../core/stores/locale.store';
import { PreferencesToggleComponent } from '../../shared/preferences-toggle.component';

/** Feature 5 — separate admin sign-in for the web panel. */
@Component({
  selector: 'app-admin-login',
  imports: [
    IonContent, IonItem, IonLabel, IonInput, IonButton, IonNote, IonSpinner,
    FormsModule, RouterLink, PreferencesToggleComponent,
  ],
  template: `
    <ion-content class="ion-padding">
      <div class="panel">
        <div class="prefs"><app-preferences-toggle /></div>

        <h1>{{ locale.t('common.appName') }}</h1>
        <p class="sub">{{ locale.t('admin.login.title') }}</p>

        <ion-item>
          <ion-label position="stacked">{{ locale.t('admin.login.email') }}</ion-label>
          <ion-input type="email" [(ngModel)]="email" inputmode="email"
                     autocomplete="email" (keyup.enter)="submit()" />
        </ion-item>

        <ion-item>
          <ion-label position="stacked">{{ locale.t('admin.login.password') }}</ion-label>
          <ion-input type="password" [(ngModel)]="password"
                     autocomplete="current-password" (keyup.enter)="submit()" />
        </ion-item>

        @if (auth.error(); as error) {
          <ion-note color="danger" class="error">{{ error }}</ion-note>
        }

        <ion-button expand="block" [disabled]="auth.loading()" (click)="submit()">
          @if (auth.loading()) { <ion-spinner name="dots" /> } @else { {{ locale.t('admin.login.signInButton') }} }
        </ion-button>

        <p class="alt"><a routerLink="/login">{{ locale.t('admin.login.clientSignIn') }}</a></p>
      </div>
    </ion-content>
  `,
  styles: `
    .panel { max-width: 400px; margin: 6vh auto 0; }
    .prefs { display: flex; justify-content: center; margin-bottom: 24px; }
    h1 { font-size: 1.6rem; margin-bottom: 0; text-align: center; }
    .sub { color: var(--ion-color-medium); margin-top: 4px; text-align: center; }
    ion-button { margin-top: 24px; }
    .error { display: block; margin-top: 16px; white-space: pre-line; }
    .alt { text-align: center; margin-top: 24px; font-size: 0.9rem; }
  `,
})
export class AdminLoginPage {
  protected readonly auth = inject(AuthStore);
  protected readonly locale = inject(LocaleStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected email = '';
  protected password = '';

  protected async submit(): Promise<void> {
    if (!this.email || !this.password) return;

    try {
      await this.auth.adminLogin(this.email, this.password);
      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/admin';
      await this.router.navigateByUrl(returnUrl, { replaceUrl: true });
    } catch {
      // Message already on auth.error(); a client account gets a clear 403 here.
    }
  }
}
