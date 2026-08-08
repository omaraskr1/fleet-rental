import { Injectable, computed, signal } from '@angular/core';

export type ThemePreference = 'light' | 'dark' | 'system';

const THEME_KEY = 'fleet_rental_theme';
const DARK_CLASS = 'ion-palette-dark';

/**
 * Light/dark theme, independent of the OS setting.
 */
@Injectable({ providedIn: 'root' })
export class ThemeStore {
  /**
   * Guarded rather than called directly: `window.matchMedia` is absent in
   * jsdom (the unit-test DOM) and in any non-browser render context, and this
   * runs eagerly at construction. Without the guard, injecting this store
   * anywhere without a full browser crashes immediately rather than degrading
   * to "assume light".
   */
  private readonly media = typeof window.matchMedia === 'function'
    ? window.matchMedia('(prefers-color-scheme: dark)')
    : null;

  private readonly _preference = signal<ThemePreference>('system');
  private readonly _systemPrefersDark = signal(this.media?.matches ?? false);

  readonly preference = this._preference.asReadonly();

  /** What's actually rendered right now, resolving "system" to a concrete value. */
  readonly effective = computed<'light' | 'dark'>(() => {
    const pref = this._preference();
    return pref === 'system' ? (this._systemPrefersDark() ? 'dark' : 'light') : pref;
  });

  readonly isDark = computed(() => this.effective() === 'dark');

  constructor() {
    // Tracks the OS setting live, so a user who never touched the toggle keeps
    // following the system even if it changes (e.g. sunset-triggered dark mode)
    // while their session is open.
    this.media?.addEventListener('change', (e) => this._systemPrefersDark.set(e.matches));
  }

  /** Reads the stored preference and applies it, before the app's first paint. */
  init(): void {
    const stored = localStorage.getItem(THEME_KEY) as ThemePreference | null;
    this.setPreference(stored ?? 'system');
  }

  setPreference(preference: ThemePreference): void {
    this._preference.set(preference);
    localStorage.setItem(THEME_KEY, preference);
    this.applyToDocument();
  }

  /** Cycles light -> dark -> system, for a single tap toggle in the UI. */
  cycle(): void {
    const next: Record<ThemePreference, ThemePreference> = {
      light: 'dark',
      dark: 'system',
      system: 'light',
    };
    this.setPreference(next[this._preference()]);
  }

  /**
   * Applying and reading effective() both need to happen after every change —
   * including OS-driven ones — so this runs from an effect-free spot both
   * setPreference and the media-query listener can call directly.
   */
  private applyToDocument(): void {
    document.documentElement.classList.toggle(DARK_CLASS, this.effective() === 'dark');
  }
}
