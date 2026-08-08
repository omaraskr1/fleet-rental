import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  IonBadge, IonButton, IonCard, IonCardContent, IonInput, IonItem, IonLabel, IonNote, IonSpinner,
} from '@ionic/angular/standalone';

import { PlatformCompaniesStore } from '../../core/stores/platform-companies.store';
import { LocaleStore } from '../../core/stores/locale.store';

/** Every company on the platform: add new ones, suspend/reactivate, and jump to their admins. */
@Component({
  selector: 'app-platform-companies',
  imports: [
    IonCard, IonCardContent, IonItem, IonLabel, IonInput, IonButton, IonBadge, IonNote, IonSpinner,
    FormsModule,
  ],
  template: `
    <h1>{{ locale.t('platform.companies.title') }}</h1>

    <ion-card>
      <ion-card-content>
        <h2>{{ locale.t('platform.companies.form.title') }}</h2>
        <div class="inline-form">
          <ion-item>
            <ion-label position="stacked">{{ locale.t('platform.companies.form.name') }}</ion-label>
            <ion-input [(ngModel)]="newName" />
          </ion-item>
          <ion-item>
            <ion-label position="stacked">{{ locale.t('platform.companies.form.code') }}</ion-label>
            <ion-input [(ngModel)]="newCode" placeholder="gulf-fleet" />
          </ion-item>
          <ion-item>
            <ion-label position="stacked">{{ locale.t('platform.companies.form.contactEmailOptional') }}</ion-label>
            <ion-input type="email" [(ngModel)]="newContactEmail" />
          </ion-item>
          <ion-button [disabled]="!isValid() || store.submitting()" (click)="submit()">
            @if (store.submitting()) { <ion-spinner name="dots" /> } @else { {{ locale.t('platform.companies.form.save') }} }
          </ion-button>
        </div>
        <p class="hint">{{ locale.t('platform.companies.form.codeHelper') }}</p>
      </ion-card-content>
    </ion-card>

    @if (store.error(); as error) {
      <ion-note color="danger" class="banner">{{ error }}</ion-note>
    }

    @if (store.loading() && store.companies().length === 0) {
      <div class="state"><ion-spinner /></div>
    } @else {
      <div class="scroller">
        <table>
          <thead>
            <tr>
              <th>{{ locale.t('platform.companies.name') }}</th>
              <th>{{ locale.t('platform.companies.code') }}</th>
              <th>{{ locale.t('platform.companies.contactEmail') }}</th>
              <th>{{ locale.t('platform.companies.status') }}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (company of store.companies(); track company.id) {
              <tr>
                <td class="name">{{ company.name }}</td>
                <td><code>{{ company.code }}</code></td>
                <td>{{ company.contactEmail || '—' }}</td>
                <td>
                  <ion-badge [color]="company.status === 'Active' ? 'success' : 'medium'">
                    {{ company.status === 'Active' ? locale.t('platform.companies.active') : locale.t('platform.companies.suspended') }}
                  </ion-badge>
                </td>
                <td class="actions">
                  <ion-button size="small" fill="clear" (click)="manageAdmins(company.id)">
                    {{ locale.t('platform.companies.manageAdmins') }}
                  </ion-button>
                  <ion-button size="small" fill="clear" (click)="manageFeatures(company.id)">
                    {{ locale.t('platform.companies.manageFeatures') }}
                  </ion-button>
                  @if (company.status === 'Active') {
                    <ion-button size="small" fill="clear" color="danger" (click)="suspend(company.id)">
                      {{ locale.t('platform.companies.suspend') }}
                    </ion-button>
                  } @else {
                    <ion-button size="small" fill="clear" (click)="reactivate(company.id)">
                      {{ locale.t('platform.companies.reactivate') }}
                    </ion-button>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: `
    h1 { font-size: 1.4rem; margin: 0 0 16px; }
    h2 { font-size: 1rem; margin: 0; }
    .inline-form { display: grid; grid-template-columns: 1fr 1fr 1fr auto; gap: 8px; align-items: end; }
    .hint { color: var(--ion-color-medium); font-size: 0.8rem; margin: 8px 0 0; }
    .scroller { overflow-x: auto; margin-top: 20px; }
    table { border-collapse: collapse; width: 100%; font-size: 0.9rem; }
    th { text-align: start; font-weight: 500; color: var(--ion-color-medium);
         font-size: 0.75rem; text-transform: uppercase; padding-block: 8px; padding-inline-end: 12px; }
    td { padding-block: 10px; padding-inline-end: 12px;
         border-top: 1px solid var(--ion-color-light-shade); white-space: nowrap; }
    td.name { font-weight: 500; }
    td.actions { display: flex; gap: 4px; align-items: center; }
    code { font-size: 0.85rem; color: var(--ion-color-medium); }
    .state { padding: 48px; text-align: center; }
    .banner { display: block; padding: 8px 0; white-space: pre-line; }
  `,
})
export class PlatformCompaniesPage implements OnInit {
  protected readonly store = inject(PlatformCompaniesStore);
  protected readonly locale = inject(LocaleStore);
  private readonly router = inject(Router);

  protected newName = '';
  protected newCode = '';
  protected newContactEmail = '';

  ngOnInit(): void {
    void this.store.loadCompanies();
  }

  /** A plain method, not computed() — see BUG-001 in BUGS.md for why. */
  protected isValid(): boolean {
    return this.newName.trim().length > 0 && this.newCode.trim().length >= 3;
  }

  protected async submit(): Promise<void> {
    if (!this.isValid()) {
      return;
    }

    await this.store.createCompany({
      name: this.newName.trim(),
      code: this.newCode.trim(),
      contactEmail: this.newContactEmail.trim() || null,
    });

    this.newName = '';
    this.newCode = '';
    this.newContactEmail = '';
  }

  protected async suspend(tenantId: string): Promise<void> {
    await this.store.suspendCompany(tenantId);
  }

  protected async reactivate(tenantId: string): Promise<void> {
    await this.store.reactivateCompany(tenantId);
  }

  protected manageAdmins(tenantId: string): void {
    void this.router.navigate(['/platform/companies', tenantId, 'admins']);
  }

  protected manageFeatures(tenantId: string): void {
    void this.router.navigate(['/platform/companies', tenantId, 'features']);
  }
}
