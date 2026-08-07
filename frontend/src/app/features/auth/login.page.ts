import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  IonButton, IonContent, IonHeader, IonInput, IonItem, IonLabel, IonNote,
  IonSpinner, IonTitle, IonToolbar,
} from '@ionic/angular/standalone';

import { AuthStore } from '../../core/stores/auth.store';
import { PushService } from '../../core/services/push.service';

/** Feature 5 — client login. */
@Component({
  selector: 'app-login',
  imports: [
    IonHeader, IonToolbar, IonTitle, IonContent, IonItem, IonLabel, IonInput,
    IonButton, IonNote, IonSpinner, FormsModule, RouterLink,
  ],
  template: `
    <ion-header>
      <ion-toolbar><ion-title>Sign in</ion-title></ion-toolbar>
    </ion-header>

    <ion-content class="ion-padding">
      <h1>Welcome back</h1>
      <p class="sub">Sign in to request vehicles for your events.</p>

      <ion-item>
        <ion-label position="stacked">Email</ion-label>
        <ion-input
          type="email"
          [(ngModel)]="email"
          autocomplete="email"
          inputmode="email"
          (keyup.enter)="submit()" />
      </ion-item>

      <ion-item>
        <ion-label position="stacked">Password</ion-label>
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
        @if (auth.loading()) { <ion-spinner name="dots" /> } @else { Sign in }
      </ion-button>

      <p class="alt">
        No account? <a routerLink="/signup">Create one</a>
      </p>
      <p class="alt muted">
        Fleet owner? <a routerLink="/admin/login">Admin sign in</a>
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
