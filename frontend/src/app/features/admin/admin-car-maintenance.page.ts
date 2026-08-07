import { Component, inject, input, OnInit } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  IonBadge, IonButton, IonCard, IonCardContent, IonIcon, IonInput, IonItem,
  IonLabel, IonNote, IonSelect, IonSelectOption, IonSpinner,
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { warningOutline } from 'ionicons/icons';

import { MaintenanceStore } from '../../core/stores/maintenance.store';
import { LocaleStore } from '../../core/stores/locale.store';
import { ISSUE_SEVERITIES, type IssueSeverity } from '../../core/models';

/**
 * Per-car mechanical detail: current odometer, service interval, full service
 * history, and issue reporting — the "insights and vehicle issue" screen an
 * owner needs before a car goes out on its next booking.
 */
@Component({
  selector: 'app-admin-car-maintenance',
  imports: [
    IonCard, IonCardContent, IonBadge, IonButton, IonInput, IonItem, IonLabel,
    IonSelect, IonSelectOption, IonNote, IonSpinner, IonIcon,
    FormsModule, CurrencyPipe,
  ],
  template: `
    <h1>{{ locale.t('admin.maintenance.title') }}</h1>

    @if (store.summary(); as summary) {
      <ion-card>
        <ion-card-content>
          <h2>{{ summary.carName }}</h2>

          <div class="grid">
            <div>
              <small>{{ locale.t('admin.maintenance.currentOdometer') }}</small>
              <p>
                {{ summary.currentOdometerKm ?? locale.t('admin.maintenance.notTracked') }}
                @if (summary.currentOdometerKm !== null) { {{ locale.t('admin.maintenance.km') }} }
              </p>
            </div>
            <div>
              <small>{{ locale.t('admin.maintenance.serviceInterval') }}</small>
              <p>
                {{ summary.serviceIntervalKm ?? locale.t('admin.maintenance.notTracked') }}
                @if (summary.serviceIntervalKm !== null) { {{ locale.t('admin.maintenance.km') }} }
              </p>
            </div>
            <div>
              <small>{{ locale.t('admin.maintenance.lastService') }}</small>
              <p>
                @if (summary.lastServiceAt) {
                  {{ locale.formatDate(summary.lastServiceAt, { day: 'numeric', month: 'short', year: 'numeric' }) }}
                } @else {
                  {{ locale.t('admin.maintenance.never') }}
                }
              </p>
            </div>
          </div>

          <div class="badges">
            <ion-badge [color]="summary.isServiceDue ? 'warning' : 'success'">
              {{ summary.isServiceDue ? locale.t('admin.maintenance.due') : locale.t('admin.maintenance.notDue') }}
            </ion-badge>
            @if (summary.hasBlockingIssue) {
              <ion-badge color="danger">
                <ion-icon name="warning-outline" /> {{ locale.t('enums.issueSeverity.Critical') }}
              </ion-badge>
            }
          </div>

          <div class="inline-form">
            <ion-item>
              <ion-label position="stacked">{{ locale.t('admin.maintenance.updateOdometer') }}</ion-label>
              <ion-input type="number" [(ngModel)]="odometerInput" [placeholder]="locale.t('admin.maintenance.km')" />
            </ion-item>
            <ion-button size="small" [disabled]="!odometerInput || store.submitting()" (click)="saveOdometer()">
              {{ locale.t('admin.maintenance.save') }}
            </ion-button>

            <ion-item>
              <ion-label position="stacked">{{ locale.t('admin.maintenance.updateInterval') }}</ion-label>
              <ion-input type="number" [(ngModel)]="intervalInput" [placeholder]="locale.t('admin.maintenance.km')" />
            </ion-item>
            <ion-button size="small" [disabled]="store.submitting()" (click)="saveInterval()">
              {{ locale.t('admin.maintenance.save') }}
            </ion-button>
          </div>
        </ion-card-content>
      </ion-card>
    }

    @if (store.error(); as error) {
      <ion-note color="danger" class="banner">{{ error }}</ion-note>
    }

    <ion-card>
      <ion-card-content>
        <h2>{{ locale.t('admin.maintenance.logService') }}</h2>

        <ion-item>
          <ion-label position="stacked">{{ locale.t('admin.maintenance.date') }}</ion-label>
          <ion-input type="date" [(ngModel)]="serviceDate" />
        </ion-item>
        <ion-item>
          <ion-label position="stacked">{{ locale.t('admin.maintenance.description') }}</ion-label>
          <ion-input [(ngModel)]="serviceDescription" [placeholder]="locale.t('admin.maintenance.descriptionPlaceholder')" />
        </ion-item>
        <ion-item>
          <ion-label position="stacked">{{ locale.t('admin.maintenance.odometerOptional') }}</ion-label>
          <ion-input type="number" [(ngModel)]="serviceOdometer" />
        </ion-item>
        <ion-item>
          <ion-label position="stacked">{{ locale.t('admin.maintenance.cost') }}</ion-label>
          <ion-input type="number" [(ngModel)]="serviceCost" />
        </ion-item>
        <ion-item>
          <ion-label position="stacked">{{ locale.t('admin.maintenance.performedByOptional') }}</ion-label>
          <ion-input [(ngModel)]="servicePerformedBy" [placeholder]="locale.t('admin.maintenance.performedByPlaceholder')" />
        </ion-item>

        <ion-button expand="block" [disabled]="!canSubmitService() || store.submitting()" (click)="submitService()">
          @if (store.submitting()) { <ion-spinner name="dots" /> } @else { {{ locale.t('admin.maintenance.submitService') }} }
        </ion-button>
      </ion-card-content>
    </ion-card>

    <h2 class="section">{{ locale.t('admin.maintenance.serviceHistory') }}</h2>
    @if (store.history().length === 0) {
      <p class="empty">{{ locale.t('admin.maintenance.noHistory') }}</p>
    } @else {
      @for (record of store.history(); track record.id) {
        <ion-card>
          <ion-card-content>
            <div class="row">
              <strong>{{ locale.formatDate(record.performedAt, { day: 'numeric', month: 'short', year: 'numeric' }) }}</strong>
              <span>{{ record.cost | currency: 'USD' : 'symbol' : '1.0-0' }}</span>
            </div>
            <p>{{ record.description }}</p>
            <p class="muted">
              @if (record.odometerKm) { {{ record.odometerKm }} {{ locale.t('admin.maintenance.km') }} }
              @if (record.performedBy) { · {{ record.performedBy }} }
            </p>
          </ion-card-content>
        </ion-card>
      }
    }

    <h2 class="section">{{ locale.t('admin.maintenance.issuesForCar') }}</h2>

    <ion-card>
      <ion-card-content>
        <h2>{{ locale.t('admin.maintenance.reportIssue') }}</h2>
        <ion-item>
          <ion-label position="stacked">{{ locale.t('admin.maintenance.description') }}</ion-label>
          <ion-input [(ngModel)]="issueDescription" [placeholder]="locale.t('admin.maintenance.issueDescriptionPlaceholder')" />
        </ion-item>
        <ion-item>
          <ion-label position="stacked">{{ locale.t('admin.maintenance.severity') }}</ion-label>
          <ion-select [(ngModel)]="issueSeverity" interface="action-sheet">
            @for (severity of severities; track severity) {
              <ion-select-option [value]="severity">{{ locale.t('enums.issueSeverity.' + severity) }}</ion-select-option>
            }
          </ion-select>
        </ion-item>
        <ion-button expand="block" [disabled]="!issueDescription.trim() || store.submitting()" (click)="submitIssue()">
          {{ locale.t('admin.maintenance.submitIssue') }}
        </ion-button>
      </ion-card-content>
    </ion-card>

    @if (store.issues().length === 0) {
      <p class="empty">{{ locale.t('admin.maintenance.noIssues') }}</p>
    } @else {
      @for (issue of store.issues(); track issue.id) {
        <ion-card>
          <ion-card-content>
            <div class="row">
              <ion-badge [color]="severityColor(issue.severity)">{{ locale.t('enums.issueSeverity.' + issue.severity) }}</ion-badge>
              <ion-badge color="medium">{{ locale.t('enums.issueStatus.' + issue.status) }}</ion-badge>
            </div>
            <p>{{ issue.description }}</p>
            @if (issue.status !== 'Resolved') {
              <div class="actions">
                @if (issue.status === 'Open') {
                  <ion-button size="small" (click)="startProgress(issue.id)">
                    {{ locale.t('admin.issues.startProgress') }}
                  </ion-button>
                }
                <ion-button size="small" color="success" (click)="resolve(issue.id)">
                  {{ locale.t('admin.issues.resolve') }}
                </ion-button>
              </div>
            } @else {
              <ion-button size="small" fill="outline" (click)="reopen(issue.id)">
                {{ locale.t('admin.issues.reopen') }}
              </ion-button>
            }
          </ion-card-content>
        </ion-card>
      }
    }
  `,
  styles: `
    h1 { font-size: 1.4rem; margin: 0 0 16px; }
    h2 { font-size: 1rem; margin: 0 0 12px; }
    h2.section { margin-top: 24px; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 12px; }
    small { color: var(--ion-color-medium); font-size: 0.72rem; text-transform: uppercase; }
    .grid p { margin: 2px 0; }
    .badges { display: flex; gap: 8px; margin-top: 14px; }
    .inline-form { margin-top: 16px; display: grid; grid-template-columns: 1fr auto; gap: 8px; align-items: end; }
    .row { display: flex; justify-content: space-between; align-items: center; gap: 8px; }
    .muted { color: var(--ion-color-medium); font-size: 0.85rem; }
    .actions { display: flex; gap: 8px; margin-top: 10px; }
    .empty { color: var(--ion-color-medium); padding: 8px 0; }
    .banner { display: block; padding: 8px 0; white-space: pre-line; }
  `,
})
export class AdminCarMaintenancePage implements OnInit {
  readonly carId = input.required<string>();

  protected readonly store = inject(MaintenanceStore);
  protected readonly locale = inject(LocaleStore);
  protected readonly severities = ISSUE_SEVERITIES;

  protected odometerInput: number | null = null;
  protected intervalInput: number | null = null;

  protected serviceDate = new Date().toISOString().slice(0, 10);
  protected serviceDescription = '';
  protected serviceOdometer: number | null = null;
  protected serviceCost: number | null = null;
  protected servicePerformedBy = '';

  protected issueDescription = '';
  protected issueSeverity: IssueSeverity = 'Medium';

  constructor() {
    addIcons({ warningOutline });
  }

  async ngOnInit(): Promise<void> {
    await Promise.all([
      this.store.loadSummary(this.carId()),
      this.store.loadHistory(this.carId()),
      this.store.loadIssues(this.carId()),
    ]);
  }

  protected canSubmitService(): boolean {
    return this.serviceDescription.trim().length > 0 && this.serviceCost !== null && !!this.serviceDate;
  }

  protected severityColor(severity: string): string {
    switch (severity) {
      case 'Critical':
        return 'danger';
      case 'High':
        return 'warning';
      default:
        return 'medium';
    }
  }

  protected async saveOdometer(): Promise<void> {
    if (this.odometerInput === null) return;
    await this.store.updateOdometer(this.carId(), this.odometerInput);
    this.odometerInput = null;
  }

  protected async saveInterval(): Promise<void> {
    await this.store.setServiceInterval(this.carId(), this.intervalInput);
  }

  protected async submitService(): Promise<void> {
    if (!this.canSubmitService()) return;

    try {
      await this.store.logService(this.carId(), {
        performedAt: this.serviceDate,
        description: this.serviceDescription.trim(),
        odometerKm: this.serviceOdometer,
        cost: this.serviceCost!,
        performedBy: this.servicePerformedBy.trim() || null,
      });

      this.serviceDescription = '';
      this.serviceOdometer = null;
      this.serviceCost = null;
      this.servicePerformedBy = '';
    } catch {
      // Message shown in the banner.
    }
  }

  protected async submitIssue(): Promise<void> {
    if (!this.issueDescription.trim()) return;

    try {
      await this.store.reportIssue(this.carId(), {
        description: this.issueDescription.trim(),
        severity: this.issueSeverity,
      });

      this.issueDescription = '';
    } catch {
      // Message shown in the banner.
    }
  }

  protected async startProgress(issueId: string): Promise<void> {
    await this.store.startProgress(issueId);
  }

  protected async resolve(issueId: string): Promise<void> {
    await this.store.resolve(issueId);
  }

  protected async reopen(issueId: string): Promise<void> {
    await this.store.reopen(issueId);
  }
}
