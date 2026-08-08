import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { IonButton, IonContent } from '@ionic/angular/standalone';

import { PlatformAuthStore } from '../../core/stores/platform-auth.store';
import { LocaleStore } from '../../core/stores/locale.store';
import { PreferencesToggleComponent } from '../../shared/preferences-toggle.component';

/**
 * Platform panel shell — the super-admin tier above every tenant's own admin
 * panel. Same sidebar/outlet layout as AdminShellPage, kept as a separate
 * component rather than a shared one: the two operate on entirely different
 * identities and it should never be possible to confuse which panel a screen
 * belongs to just by looking at the URL or the sidebar.
 */
@Component({
  selector: 'app-platform-shell',
  imports: [IonContent, IonButton, RouterOutlet, RouterLink, RouterLinkActive, PreferencesToggleComponent],
  template: `
    <ion-content>
      <div class="layout">
        <aside>
          <div class="brand">
            <strong>{{ locale.t('common.appName') }}</strong>
            <small>{{ auth.admin()?.fullName }}</small>
          </div>

          <nav>
            <a routerLink="companies" routerLinkActive="active">{{ locale.t('platform.shell.companies') }}</a>
            <a routerLink="cars" routerLinkActive="active">{{ locale.t('platform.shell.cars') }}</a>
          </nav>

          <app-preferences-toggle />

          <ion-button fill="clear" size="small" (click)="signOut()">
            {{ locale.t('platform.shell.signOut') }}
          </ion-button>
        </aside>

        <main>
          <router-outlet />
        </main>
      </div>
    </ion-content>
  `,
  styles: `
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

    @media (max-width: 768px) {
      .layout { grid-template-columns: 1fr; }
      aside { border-inline-end: none; border-block-end: 1px solid var(--ion-color-light-shade); }
      nav { flex-direction: row; flex-wrap: wrap; }
      main { padding: 16px; }
    }
  `,
})
export class PlatformShellPage {
  protected readonly auth = inject(PlatformAuthStore);
  protected readonly locale = inject(LocaleStore);
  private readonly router = inject(Router);

  protected signOut(): void {
    this.auth.signOut();
    void this.router.navigate(['/platform/login'], { replaceUrl: true });
  }
}
