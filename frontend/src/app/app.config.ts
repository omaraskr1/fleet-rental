import {
  ApplicationConfig,
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
  ],
};
