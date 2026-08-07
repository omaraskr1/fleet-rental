import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import {
  IonButton, IonContent, IonHeader, IonItem, IonLabel, IonList, IonNote,
  IonTitle, IonToolbar,
} from '@ionic/angular/standalone';

import { AuthStore } from '../../core/stores/auth.store';
import { PushService } from '../../core/services/push.service';
import { TenantStore } from '../../core/stores/tenant.store';

@Component({
  selector: 'app-profile',
  imports: [
    IonHeader, IonToolbar, IonTitle, IonContent, IonList, IonItem, IonLabel,
    IonButton, IonNote,
  ],
  template: `
    <ion-header>
      <ion-toolbar><ion-title>Profile</ion-title></ion-toolbar>
    </ion-header>

    <ion-content>
      <ion-note class="company">Booking with {{ tenant.name() }}</ion-note>

      @if (auth.user(); as user) {
        <ion-list lines="full">
          <ion-item><ion-label><small>Name</small><p>{{ user.fullName }}</p></ion-label></ion-item>
          <ion-item><ion-label><small>Email</small><p>{{ user.email }}</p></ion-label></ion-item>
          @if (user.phoneNumber) {
            <ion-item><ion-label><small>Phone</small><p>{{ user.phoneNumber }}</p></ion-label></ion-item>
          }
        </ion-list>

        <div class="actions">
          @if (auth.isAdmin()) {
            <ion-button expand="block" fill="outline" (click)="goToAdmin()">
              Open admin panel
            </ion-button>
          }
          <ion-button expand="block" color="medium" fill="outline" (click)="signOut()">
            Sign out
          </ion-button>
        </div>
      } @else {
        <div class="empty">
          <ion-note>You're not signed in.</ion-note>
          <ion-button expand="block" (click)="goToLogin()">Sign in</ion-button>
        </div>
      }

      <div class="switch">
        <ion-button expand="block" fill="clear" size="small" (click)="switchCompany()">
          Not {{ tenant.name() }}? Switch company
        </ion-button>
      </div>
    </ion-content>
  `,
  styles: `
    .company { display: block; text-align: center; padding: 12px 16px 0; font-size: 0.85rem; }
    small { color: var(--ion-color-medium); font-size: 0.75rem; }
    .actions { padding: 24px 16px; display: flex; flex-direction: column; gap: 12px; }
    .empty { padding: 64px 24px; text-align: center; display: flex;
             flex-direction: column; gap: 16px; }
    .switch { padding: 0 16px 24px; }
  `,
})
export class ProfilePage {
  protected readonly auth = inject(AuthStore);
  protected readonly tenant = inject(TenantStore);
  private readonly push = inject(PushService);
  private readonly router = inject(Router);

  protected async signOut(): Promise<void> {
    // Unregister first: once the token is gone the API call would be rejected,
    // leaving a dead device row that still receives this user's pushes.
    await this.push.unregister();
    this.auth.signOut();
    await this.router.navigate(['/tabs/cars'], { replaceUrl: true });
  }

  protected async switchCompany(): Promise<void> {
    // A different company means a different account, so the session goes with it
    // — staying signed in would carry one business's token into another's screens
    // for the instant before the tenant guard catches it.
    await this.push.unregister();
    this.auth.signOut();
    this.tenant.clear();
    await this.router.navigate(['/select-company'], { replaceUrl: true });
  }

  protected goToAdmin(): void {
    void this.router.navigate(['/admin']);
  }

  protected goToLogin(): void {
    void this.router.navigate(['/login']);
  }
}
