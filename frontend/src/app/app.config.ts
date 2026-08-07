import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  provideZonelessChangeDetection,
} from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideIonicAngular } from '@ionic/angular/standalone';

import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { tenantInterceptor } from './core/interceptors/tenant.interceptor';
import { AuthStore } from './core/stores/auth.store';
import { TenantStore } from './core/stores/tenant.store';
import { LocaleStore } from './core/stores/locale.store';
import { ThemeStore } from './core/stores/theme.store';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),

    // Signals drive every store in this app, so there is nothing left for Zone.js
    // to patch. Running zoneless drops the polyfill and the change-detection
    // overhead that goes with it — worth more on a phone than on a desktop.
    provideZonelessChangeDetection(),

    provideRouter(routes, withComponentInputBinding()),

    // Order matters: tenant scopes the request, auth attaches the token, then
    // error maps failures. The error interceptor sits outermost so it sees
    // responses to fully-formed requests.
    provideHttpClient(withInterceptors([tenantInterceptor, authInterceptor, errorInterceptor])),

    provideIonicAngular({
      // Render iOS styling on iOS and Material on Android, which is what makes
      // the app feel native rather than like one web app on two platforms.
      mode: undefined,
      rippleEffect: true,
    }),

    /**
     * Restores tenant, theme, locale and session BEFORE the router's initial
     * navigation runs.
     *
     * This used to happen in App.ngOnInit, which runs on component
     * construction — by which point the router had already, in practice,
     * started evaluating guards for the initial URL. tenantGuard and authGuard
     * read these stores' signals synchronously, so a returning user with a
     * saved company and a valid session was routinely bounced to
     * "select your company" or "sign in" simply because the guard ran before
     * the restore did. provideAppInitializer blocks bootstrap — and therefore
     * the router's first navigation — until the returned promise settles,
     * which is the actual guarantee this needs rather than a hopeful ordering
     * of synchronous statements ahead of an eventual await.
     */
    provideAppInitializer(async () => {
      const theme = inject(ThemeStore);
      const tenant = inject(TenantStore);
      const locale = inject(LocaleStore);
      const auth = inject(AuthStore);

      theme.init();
      tenant.restore();

      await locale.init();
      await auth.restoreSession();
    }),
  ],
};
