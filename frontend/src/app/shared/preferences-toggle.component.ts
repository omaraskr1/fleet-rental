import { Component, inject } from '@angular/core';
import { IonButton, IonIcon, IonLabel } from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { languageOutline, moonOutline, phonePortraitOutline, sunnyOutline } from 'ionicons/icons';

import { LocaleStore } from '../core/stores/locale.store';
import { ThemeStore } from '../core/stores/theme.store';

/**
 * Language and theme controls, shared by the client profile page and the admin
 * shell so the two surfaces cannot drift into offering different toggles.
 */
@Component({
  selector: 'app-preferences-toggle',
  imports: [IonButton, IonIcon, IonLabel],
  template: `
    <div class="row">
      <ion-button fill="outline" size="small" (click)="locale.toggle()">
        <ion-icon slot="start" name="language-outline" />
        <ion-label>{{ locale.locale() === 'en' ? 'العربية' : 'English' }}</ion-label>
      </ion-button>

      <ion-button fill="outline" size="small" (click)="theme.cycle()">
        <ion-icon slot="start" [name]="themeIcon()" />
        <ion-label>{{ locale.t('theme.' + theme.preference()) }}</ion-label>
      </ion-button>
    </div>
  `,
  styles: `
    .row { display: flex; gap: 8px; flex-wrap: wrap; }
  `,
})
export class PreferencesToggleComponent {
  protected readonly locale = inject(LocaleStore);
  protected readonly theme = inject(ThemeStore);

  constructor() {
    addIcons({ languageOutline, sunnyOutline, moonOutline, phonePortraitOutline });
  }

  protected themeIcon(): string {
    switch (this.theme.preference()) {
      case 'light':
        return 'sunny-outline';
      case 'dark':
        return 'moon-outline';
      default:
        return 'phone-portrait-outline';
    }
  }
}
