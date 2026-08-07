import { Component, inject, OnInit } from '@angular/core';
import { IonApp, IonRouterOutlet } from '@ionic/angular/standalone';

import { AuthStore } from './core/stores/auth.store';
import { PushService } from './core/services/push.service';

@Component({
  selector: 'app-root',
  imports: [IonApp, IonRouterOutlet],
  template: `
    <ion-app>
      <ion-router-outlet />
    </ion-app>
  `,
})
export class App implements OnInit {
  private readonly auth = inject(AuthStore);
  private readonly push = inject(PushService);

  async ngOnInit(): Promise<void> {
    // Tenant, theme, locale and session are all resolved before this ever
    // runs — provideAppInitializer in app.config.ts blocks the router's
    // initial navigation until they are, which is what guards need. Push
    // registration doesn't gate any route, so it stays here rather than
    // delaying first paint for a permission prompt.
    if (this.auth.isAuthenticated()) {
      await this.push.register();
    }
  }
}
