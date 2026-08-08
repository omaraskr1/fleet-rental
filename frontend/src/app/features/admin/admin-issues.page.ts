import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  IonBadge, IonButton, IonCard, IonCardContent, IonNote, IonSegment,
  IonSegmentButton, IonLabel, IonSpinner, IonTextarea,
} from '@ionic/angular/standalone';

import { MaintenanceStore } from '../../core/stores/maintenance.store';
import { LocaleStore } from '../../core/stores/locale.store';
import type { IssueStatus } from '../../core/models';

/**
 * Fleet-wide open issues — the owner's "does anything need attention before it
 * goes out again" view, across every vehicle rather than one at a time.
 */
@Component({
  selector: 'app-admin-issues',
  imports: [
    IonSegment, IonSegmentButton, IonLabel, IonCard, IonCardContent, IonBadge,
    IonButton, IonTextarea, IonNote, IonSpinner, FormsModule,
  ],
  template: `
    <h1>{{ locale.t('admin.issues.title') }}</h1>

    <ion-segment [value]="filter()" (ionChange)="onFilter($event)">
      <ion-segment-button value="all"><ion-label>{{ locale.t('admin.issues.filterAll') }}</ion-label></ion-segment-button>
      <ion-segment-button value="Open"><ion-label>{{ locale.t('admin.issues.filterOpen') }}</ion-label></ion-segment-button>
      <ion-segment-button value="InProgress"><ion-label>{{ locale.t('admin.issues.filterInProgress') }}</ion-label></ion-segment-button>
      <ion-segment-button value="Resolved"><ion-label>{{ locale.t('admin.issues.filterResolved') }}</ion-label></ion-segment-button>
    </ion-segment>

    @if (store.error(); as error) {
      <ion-note color="danger" class="banner">{{ error }}</ion-note>
    }

    @if (store.loading() && store.issues().length === 0) {
      <div class="state"><ion-spinner /></div>
    } @else if (store.issues().length === 0) {
      <p class="state">{{ locale.t('admin.issues.empty') }}</p>
    } @else {
      @for (issue of store.issues(); track issue.id) {
        <ion-card>
          <ion-card-content>
            <div class="head">
              <div>
                <h2 class="link" (click)="openCar(issue.carId)">{{ issue.carName }}</h2>
                <p class="muted">
                  {{ locale.t('admin.issues.reportedBy') }}: {{ issue.reportedByName }} ·
                  {{ locale.formatDate(issue.reportedAt, { day: 'numeric', month: 'short', year: 'numeric' }) }}
                </p>
              </div>
              <div class="badges">
                <ion-badge [color]="severityColor(issue.severity)">{{ locale.t('enums.issueSeverity.' + issue.severity) }}</ion-badge>
                <ion-badge color="medium">{{ locale.t('enums.issueStatus.' + issue.status) }}</ion-badge>
              </div>
            </div>

            <p>{{ issue.description }}</p>

            @if (issue.status === 'Resolved') {
              @if (issue.resolutionNotes) {
                <p class="muted">{{ issue.resolutionNotes }}</p>
              }
              <ion-button size="small" fill="outline" (click)="reopen(issue.id)">
                {{ locale.t('admin.issues.reopen') }}
              </ion-button>
            } @else {
              @if (decidingId() === issue.id) {
                <ion-textarea
                  [(ngModel)]="resolutionNotes"
                  [placeholder]="locale.t('admin.issues.resolutionNotesPlaceholder')"
                  [autoGrow]="true"
                  fill="outline" />
                <div class="actions">
                  <ion-button size="small" color="success" (click)="confirmResolve(issue.id)">
                    {{ locale.t('admin.issues.resolve') }}
                  </ion-button>
                  <ion-button size="small" fill="clear" (click)="decidingId.set(null)">
                    {{ locale.t('common.cancel') }}
                  </ion-button>
                </div>
              } @else {
                <div class="actions">
                  @if (issue.status === 'Open') {
                    <ion-button size="small" (click)="startProgress(issue.id)">
                      {{ locale.t('admin.issues.startProgress') }}
                    </ion-button>
                  }
                  <ion-button size="small" color="success" (click)="startResolve(issue.id)">
                    {{ locale.t('admin.issues.resolve') }}
                  </ion-button>
                </div>
              }
            }
          </ion-card-content>
        </ion-card>
      }
    }
  `,
  styles: `
    h1 { font-size: 1.4rem; margin: 0 0 16px; }
    h2 { font-size: 1.05rem; margin: 0; }
    h2.link { cursor: pointer; color: var(--ion-color-primary); }
    .head { display: flex; justify-content: space-between; align-items: flex-start; gap: 12px; }
    .badges { display: flex; gap: 6px; flex-shrink: 0; }
    .muted { color: var(--ion-color-medium); font-size: 0.85rem; margin: 4px 0; }
    .actions { display: flex; gap: 8px; margin-top: 10px; flex-wrap: wrap; }
    .banner { display: block; padding: 12px 0; white-space: pre-line; }
    .state { padding: 48px; text-align: center; color: var(--ion-color-medium); }
  `,
})
export class AdminIssuesPage implements OnInit {
  protected readonly store = inject(MaintenanceStore);
  protected readonly locale = inject(LocaleStore);
  private readonly router = inject(Router);

  protected readonly filter = signal<'all' | IssueStatus>('all');
  protected readonly decidingId = signal<string | null>(null);
  protected resolutionNotes = '';

  ngOnInit(): void {
    void this.store.loadIssues();
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

  protected onFilter(event: CustomEvent): void {
    const value = event.detail.value as 'all' | IssueStatus;
    this.filter.set(value);
    void this.store.loadIssues(undefined, value === 'all' ? undefined : value);
  }

  protected openCar(carId: string): void {
    void this.router.navigate(['/admin/fleet', carId, 'maintenance']);
  }

  protected startResolve(issueId: string): void {
    this.decidingId.set(issueId);
    this.resolutionNotes = '';
  }

  protected async confirmResolve(issueId: string): Promise<void> {
    await this.store.resolve(issueId, this.resolutionNotes || undefined);
    this.decidingId.set(null);
  }

  protected async startProgress(issueId: string): Promise<void> {
    await this.store.startProgress(issueId);
  }

  protected async reopen(issueId: string): Promise<void> {
    await this.store.reopen(issueId);
  }
}
