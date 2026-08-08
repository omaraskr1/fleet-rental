import { Component, computed, inject, input, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  IonBadge, IonButton, IonCard, IonCardContent, IonInput, IonItem, IonLabel, IonNote, IonSpinner,
} from '@ionic/angular/standalone';

import { PlatformCompaniesStore } from '../../core/stores/platform-companies.store';
import { LocaleStore } from '../../core/stores/locale.store';

/** Admins for one company — reached from the Companies list's "Admins" link. */
@Component({
  selector: 'app-platform-company-admins',
  imports: [
    IonCard, IonCardContent, IonItem, IonLabel, IonInput, IonButton, IonBadge, IonNote, IonSpinner,
    FormsModule,
  ],
  template: `
    <ion-button fill="clear" size="small" (click)="back()">
      &larr; {{ locale.t('platform.companyAdmins.back') }}
    </ion-button>

    <h1>{{ locale.t('platform.companyAdmins.title', { company: companyName() }) }}</h1>

    <ion-card>
      <ion-card-content>
        <h2>{{ locale.t('platform.companyAdmins.form.title') }}</h2>
        <div class="inline-form">
          <ion-item>
            <ion-label position="stacked">{{ locale.t('platform.companyAdmins.form.fullName') }}</ion-label>
            <ion-input [(ngModel)]="newFullName" />
          </ion-item>
          <ion-item>
            <ion-label position="stacked">{{ locale.t('platform.companyAdmins.form.email') }}</ion-label>
            <ion-input type="email" [(ngModel)]="newEmail" />
          </ion-item>
          <ion-item>
            <ion-label position="stacked">{{ locale.t('platform.companyAdmins.form.password') }}</ion-label>
            <ion-input type="password" [(ngModel)]="newPassword" />
          </ion-item>
          <ion-button [disabled]="!isValid() || store.submitting()" (click)="submit()">
            @if (store.submitting()) { <ion-spinner name="dots" /> } @else { {{ locale.t('platform.companyAdmins.form.save') }} }
          </ion-button>
        </div>
      </ion-card-content>
    </ion-card>

    @if (store.error(); as error) {
      <ion-note color="danger" class="banner">{{ error }}</ion-note>
    }

    @if (store.loading() && store.admins().length === 0) {
      <div class="state"><ion-spinner /></div>
    } @else if (store.admins().length === 0) {
      <p class="state">{{ locale.t('platform.companyAdmins.empty') }}</p>
    } @else {
      <div class="scroller">
        <table>
          <thead>
            <tr>
              <th>{{ locale.t('platform.companyAdmins.name') }}</th>
              <th>{{ locale.t('platform.companyAdmins.email') }}</th>
              <th>{{ locale.t('platform.companyAdmins.status') }}</th>
            </tr>
          </thead>
          <tbody>
            @for (admin of store.admins(); track admin.id) {
              <tr>
                <td class="name">{{ admin.fullName }}</td>
                <td>{{ admin.email }}</td>
                <td>
                  <ion-badge [color]="admin.isActive ? 'success' : 'medium'">
                    {{ admin.isActive ? locale.t('platform.companyAdmins.active') : locale.t('platform.companyAdmins.inactive') }}
                  </ion-badge>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: `
    h1 { font-size: 1.4rem; margin: 8px 0 16px; }
    h2 { font-size: 1rem; margin: 0; }
    .inline-form { display: grid; grid-template-columns: 1fr 1fr 1fr auto; gap: 8px; align-items: end; }
    .scroller { overflow-x: auto; margin-top: 20px; }
    table { border-collapse: collapse; width: 100%; font-size: 0.9rem; }
    th { text-align: start; font-weight: 500; color: var(--ion-color-medium);
         font-size: 0.75rem; text-transform: uppercase; padding-block: 8px; padding-inline-end: 12px; }
    td { padding-block: 10px; padding-inline-end: 12px;
         border-top: 1px solid var(--ion-color-light-shade); white-space: nowrap; }
    td.name { font-weight: 500; }
    .state { padding: 48px; text-align: center; color: var(--ion-color-medium); }
    .banner { display: block; padding: 8px 0; white-space: pre-line; }
  `,
})
export class PlatformCompanyAdminsPage implements OnInit {
  readonly tenantId = input.required<string>();

  protected readonly store = inject(PlatformCompaniesStore);
  protected readonly locale = inject(LocaleStore);
  private readonly router = inject(Router);

  protected readonly companyName = computed(
    () => this.store.companies().find((c) => c.id === this.tenantId())?.name ?? '',
  );

  protected newFullName = '';
  protected newEmail = '';
  protected newPassword = '';

  ngOnInit(): void {
    // Companies may not be loaded yet if this page was reached directly (a
    // refresh, or a bookmarked URL) rather than via the list's link.
    if (this.store.companies().length === 0) {
      void this.store.loadCompanies();
    }

    this.store.clearAdmins();
    void this.store.loadAdmins(this.tenantId());
  }

  /** A plain method, not computed() — see BUG-001 in BUGS.md for why. */
  protected isValid(): boolean {
    return (
      this.newFullName.trim().length > 0 &&
      this.newEmail.trim().length > 0 &&
      this.newPassword.trim().length >= 8
    );
  }

  protected async submit(): Promise<void> {
    if (!this.isValid()) {
      return;
    }

    await this.store.createAdmin(this.tenantId(), {
      fullName: this.newFullName.trim(),
      email: this.newEmail.trim(),
      password: this.newPassword,
    });

    this.newFullName = '';
    this.newEmail = '';
    this.newPassword = '';
  }

  protected back(): void {
    void this.router.navigate(['/platform/companies']);
  }
}
