import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import {
  IonBackButton, IonButton, IonButtons, IonContent, IonHeader, IonInput,
  IonItem, IonLabel, IonNote, IonSpinner, IonTitle, IonToolbar,
} from '@ionic/angular/standalone';

import { AuthStore } from '../../core/stores/auth.store';
import { PushService } from '../../core/services/push.service';
import { LocaleStore } from '../../core/stores/locale.store';

/** Feature 5 — client signup. */
@Component({
  selector: 'app-signup',
  imports: [
    IonHeader, IonToolbar, IonTitle, IonButtons, IonBackButton, IonContent,
    IonItem, IonLabel, IonInput, IonButton, IonNote, IonSpinner, FormsModule, RouterLink,
  ],
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start"><ion-back-button defaultHref="/login" /></ion-buttons>
        <ion-title>{{ locale.t('auth.signup.title') }}</ion-title>
      </ion-toolbar>
    </ion-header>

    <ion-content class="ion-padding">
      <ion-item>
        <ion-label position="stacked">{{ locale.t('auth.signup.fullName') }}</ion-label>
        <ion-input [(ngModel)]="fullName" autocapitalize="words" autocomplete="name" />
      </ion-item>

      <ion-item>
        <ion-label position="stacked">{{ locale.t('auth.login.email') }}</ion-label>
        <ion-input type="email" [(ngModel)]="email" inputmode="email" autocomplete="email" />
      </ion-item>

      <ion-item>
        <ion-label position="stacked">{{ locale.t('auth.signup.phone') }}</ion-label>
        <ion-input type="tel" [(ngModel)]="phone" inputmode="tel" autocomplete="tel" />
      </ion-item>

      <ion-item>
        <ion-label position="stacked">{{ locale.t('auth.login.password') }}</ion-label>
        <ion-input type="password" [(ngModel)]="password" autocomplete="new-password" />
        <ion-note slot="helper">{{ locale.t('auth.signup.passwordHelper') }}</ion-note>
      </ion-item>

      @if (auth.error(); as error) {
        <ion-note color="danger" class="error">{{ error }}</ion-note>
      }

      <ion-button expand="block" [disabled]="!isValid() || auth.loading()" (click)="submit()">
        @if (auth.loading()) { <ion-spinner name="dots" /> } @else { {{ locale.t('auth.signup.createAccountButton') }} }
      </ion-button>

      <p class="alt">
        {{ locale.t('auth.signup.alreadyHave') }} <a routerLink="/login">{{ locale.t('auth.signup.signIn') }}</a>
      </p>
    </ion-content>
  `,
  styles: `
    ion-button { margin-top: 24px; }
    .error { display: block; margin-top: 16px; white-space: pre-line; }
    .alt { text-align: center; margin-top: 16px; }
  `,
})
export class SignupPage {
  protected readonly auth = inject(AuthStore);
  protected readonly locale = inject(LocaleStore);
  private readonly push = inject(PushService);
  private readonly router = inject(Router);

  protected fullName = '';
  protected email = '';
  protected phone = '';
  protected password = '';

  protected isValid(): boolean {
    return (
      this.fullName.trim().length > 0 &&
      this.email.includes('@') &&
      this.password.length >= 8
    );
  }

  protected async submit(): Promise<void> {
    try {
      await this.auth.signUp(this.email, this.password, this.fullName, this.phone || undefined);
      await this.push.register();
      await this.router.navigate(['/tabs/cars'], { replaceUrl: true });
    } catch {
      // Message already on auth.error().
    }
  }
}
