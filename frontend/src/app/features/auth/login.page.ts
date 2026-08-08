import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  IonButton, IonContent, IonHeader, IonInput, IonItem, IonLabel, IonNote,
  IonSpinner, IonTitle, IonToolbar,
} from '@ionic/angular/standalone';

import { AuthStore } from '../../core/stores/auth.store';
import { PushService } from '../../core/services/push.service';
import { LocaleStore } from '../../core/stores/locale.store';

/** Feature 5 — client login. */
@Component({
  selector: 'app-login',
  imports: [
    IonHeader, IonToolbar, IonTitle, IonContent, IonItem, IonLabel, IonInput,
    IonButton, IonNote, IonSpinner, FormsModule, RouterLink,
  ],
  template: `
    <ion-header>
      <ion-toolbar><ion-title>{{ locale.t('auth.login.title') }}</ion-title></ion-toolbar>
    </ion-header>

    <ion-content class="ion-padding">
      <h1>{{ locale.t('auth.login.welcome') }}</h1>
      <p class="sub">{{ locale.t('auth.login.subtitle') }}</p>

      <ion-item>
        <ion-label position="stacked">{{ locale.t('auth.login.email') }}</ion-label>
        <ion-input
          type="email"
          [(ngModel)]="email"
          autocomplete="email"
          inputmode="email"
          (keyup.enter)="submit()" />
      </ion-item>

      <ion-item>
        <ion-label position="stacked">{{ locale.t('auth.login.password') }}</ion-label>
        <ion-input
          type="password"
          [(ngModel)]="password"
          autocomplete="current-password"
          (keyup.enter)="submit()" />
      </ion-item>

      @if (auth.error(); as error) {
        <ion-note color="danger" class="error">{{ error }}</ion-note>
      }

      <ion-button expand="block" [disabled]="auth.loading()" (click)="submit()">
        @if (auth.loading()) { <ion-spinner name="dots" /> } @else { {{ locale.t('auth.login.signInButton') }} }
      </ion-button>

      <p class="alt">
        {{ locale.t('auth.login.noAccount') }} <a routerLink="/signup">{{ locale.t('auth.login.createOne') }}</a>
      </p>
      <p class="alt muted">
        {{ locale.t('auth.login.fleetOwner') }} <a routerLink="/admin/login">{{ locale.t('auth.login.adminSignIn') }}</a>
      </p>
    </ion-content>
  `,
  styles: `
    h1 { font-size: 1.5rem; margin-bottom: 4px; }
    .sub { color: var(--ion-color-medium); margin-top: 0; }
    ion-button { margin-top: 24px; }
    .error { display: block; margin-top: 16px; white-space: pre-line; }
    .alt { text-align: center; margin-top: 16px; }
    .alt.muted { color: var(--ion-color-medium); font-size: 0.9rem; }
  `,
})
export class LoginPage {
  protected readonly auth = inject(AuthStore);
  protected readonly locale = inject(LocaleStore);
  private readonly push = inject(PushService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected email = '';
  protected password = '';

  protected async submit(): Promise<void> {
    if (!this.email || !this.password) return;

    try {
      await this.auth.login(this.email, this.password);

      // Ask for push permission only after a successful sign-in — prompting on a
      // cold launch, before any value is shown, is how you get it denied.
      await this.push.register();

      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/tabs/cars';
      await this.router.navigateByUrl(returnUrl, { replaceUrl: true });
    } catch {
      // Message already on auth.error().
    }
  }
}
