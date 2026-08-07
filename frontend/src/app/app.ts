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
    // Restore the session before the first guarded route resolves, otherwise a
    // signed-in user gets bounced to login on every cold start.
    await this.auth.restoreSession();

    if (this.auth.isAuthenticated()) {
      await this.push.register();
    }
  }
}
