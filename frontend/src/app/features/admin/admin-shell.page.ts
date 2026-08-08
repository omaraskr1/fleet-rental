import { Component, inject, OnInit } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { IonBadge, IonButton, IonContent } from '@ionic/angular/standalone';

import { AuthStore } from '../../core/stores/auth.store';
import { BookingsStore } from '../../core/stores/bookings.store';
import { FeaturesStore } from '../../core/stores/features.store';
import { MaintenanceStore } from '../../core/stores/maintenance.store';
import { LocaleStore } from '../../core/stores/locale.store';
import { PreferencesToggleComponent } from '../../shared/preferences-toggle.component';

/**
 * Admin panel shell (feature 4). A sidebar layout rather than Ionic tabs — this
 * half of the app is web-only and has the horizontal room for it.
 */
@Component({
  selector: 'app-admin-shell',
  imports: [
    IonContent, IonBadge, IonButton, RouterOutlet, RouterLink, RouterLinkActive,
    PreferencesToggleComponent,
  ],
  template: `
    <ion-content>
      <div class="layout">
        <aside>
          <div class="brand">
            <strong>{{ locale.t('common.appName') }}</strong>
            <small>{{ auth.displayName() }}</small>
          </div>

          <nav>
            <a routerLink="requests" routerLinkActive="active">
              {{ locale.t('admin.shell.requests') }}
              @if (bookings.pendingCount() > 0) {
                <ion-badge color="warning">{{ bookings.pendingCount() }}</ion-badge>
              }
            </a>
            <a routerLink="calendar" routerLinkActive="active">{{ locale.t('admin.shell.calendar') }}</a>
            <a routerLink="fleet" routerLinkActive="active">{{ locale.t('admin.shell.vehicles') }}</a>
            @if (features.isEnabled('Maintenance')) {
              <a routerLink="issues" routerLinkActive="active">
                {{ locale.t('admin.issues.title') }}
                @if (maintenance.openIssueCount() > 0) {
                  <ion-badge [color]="maintenance.hasCriticalIssue() ? 'danger' : 'warning'">
                    {{ maintenance.openIssueCount() }}
                  </ion-badge>
                }
              </a>
              <a routerLink="services" routerLinkActive="active">{{ locale.t('admin.services.title') }}</a>
            }
            @if (features.isEnabled('Analytics')) {
              <a routerLink="analytics" routerLinkActive="active">{{ locale.t('admin.analytics.title') }}</a>
            }
            @if (features.isEnabled('Gps')) {
              <a routerLink="locations" routerLinkActive="active">{{ locale.t('admin.locations.title') }}</a>
            }
          </nav>

          <app-preferences-toggle />

          <ion-button fill="clear" size="small" (click)="signOut()">
            {{ locale.t('admin.shell.signOut') }}
          </ion-button>
        </aside>

        <main>
          <router-outlet />
        </main>
      </div>
    </ion-content>
  `,
  styles: `
    /* Angular components default to display:inline; without this the host
       never reports a real height, so .layout's min-height:100% resolves
       against nothing and the shorter column (aside) leaves unpainted
       canvas showing through below its content instead of the theme
       background. */
    :host { display: block; min-height: 100%; }
    .layout { display: grid; grid-template-columns: 240px 1fr; min-height: 100%;
              background: var(--ion-background-color); color: var(--ion-text-color); }
    aside { border-inline-end: 1px solid var(--ion-color-light-shade);
            padding: 20px 12px; display: flex; flex-direction: column; gap: 20px;
            background: var(--ion-background-color); }
    .brand { padding: 0 8px; display: flex; flex-direction: column; }
    .brand small { color: var(--ion-color-medium); font-size: 0.8rem; }
    nav { display: flex; flex-direction: column; gap: 2px; flex: 1; }
    nav a { display: flex; align-items: center; justify-content: space-between;
            padding: 10px 12px; border-radius: 8px; text-decoration: none;
            color: var(--ion-text-color); font-size: 0.92rem; }
    nav a:hover { background: var(--ion-color-light); }
    nav a.active { background: var(--ion-color-primary); color: #fff; font-weight: 600; }
    main { padding: 24px 28px; overflow-x: auto; }

    /* The panel is web-first, but a fleet owner checking requests from a phone
       should still get something usable rather than a squeezed sidebar. */
    @media (max-width: 768px) {
      .layout { grid-template-columns: 1fr; }
      aside { border-inline-end: none; border-block-end: 1px solid var(--ion-color-light-shade); }
      nav { flex-direction: row; flex-wrap: wrap; }
      main { padding: 16px; }
    }
  `,
})
export class AdminShellPage implements OnInit {
  protected readonly auth = inject(AuthStore);
  protected readonly bookings = inject(BookingsStore);
  protected readonly features = inject(FeaturesStore);
  protected readonly maintenance = inject(MaintenanceStore);
  protected readonly locale = inject(LocaleStore);
  private readonly router = inject(Router);

  ngOnInit(): void {
    // Loaded here so the sidebar badges are populated on every admin route, not
    // only after their own page has been visited.
    void this.bookings.loadAllBookings();
    void this.maintenance.loadIssues();
    void this.features.load();
  }

  protected signOut(): void {
    this.auth.signOut();
    void this.router.navigate(['/admin/login'], { replaceUrl: true });
  }
}
