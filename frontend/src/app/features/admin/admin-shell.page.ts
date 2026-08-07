import { Component, inject, OnInit } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { IonBadge, IonButton, IonContent } from '@ionic/angular/standalone';

import { AuthStore } from '../../core/stores/auth.store';
import { BookingsStore } from '../../core/stores/bookings.store';
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
    .layout { display: grid; grid-template-columns: 240px 1fr; min-height: 100%; }
    aside { border-inline-end: 1px solid var(--ion-color-light-shade);
            padding: 20px 12px; display: flex; flex-direction: column; gap: 20px; }
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
  protected readonly locale = inject(LocaleStore);
  private readonly router = inject(Router);

  ngOnInit(): void {
    // Loaded here so the sidebar badge is populated on every admin route, not
    // only after the requests page has been visited.
    void this.bookings.loadAllBookings();
  }

  protected signOut(): void {
    this.auth.signOut();
    void this.router.navigate(['/admin/login'], { replaceUrl: true });
  }
}
