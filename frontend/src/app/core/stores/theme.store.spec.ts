import { TestBed } from '@angular/core/testing';

import { ThemeStore } from './theme.store';

const DARK_CLASS = 'ion-palette-dark';

describe('ThemeStore', () => {
  let store: ThemeStore;

  beforeEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove(DARK_CLASS);

    TestBed.configureTestingModule({});
    store = TestBed.inject(ThemeStore);
  });

  it('constructs without throwing where matchMedia is unavailable (e.g. this jsdom test DOM)', () => {
    // ThemeStore reads window.matchMedia eagerly in a field initializer. jsdom
    // does not implement it, so this is also a regression guard: if the guard
    // in the store were ever removed, injecting it here would throw again.
    expect(store).toBeTruthy();
  });

  it('defaults to following the system, resolved as light when matchMedia is unavailable', () => {
    expect(store.preference()).toBe('system');
    expect(store.effective()).toBe('light');
    expect(store.isDark()).toBe(false);
  });

  describe('setPreference', () => {
    it('light applies the dark class as false and persists the choice', () => {
      store.setPreference('light');

      expect(store.effective()).toBe('light');
      expect(document.documentElement.classList.contains(DARK_CLASS)).toBe(false);
      expect(localStorage.getItem('fleet_rental_theme')).toBe('light');
    });

    it('dark applies the ion-palette-dark class Ionic reads for its dark CSS variables', () => {
      store.setPreference('dark');

      expect(store.effective()).toBe('dark');
      expect(store.isDark()).toBe(true);
      expect(document.documentElement.classList.contains(DARK_CLASS)).toBe(true);
    });

    it('switching back to light removes the class rather than leaving it stuck', () => {
      store.setPreference('dark');
      store.setPreference('light');

      expect(document.documentElement.classList.contains(DARK_CLASS)).toBe(false);
    });
  });

  describe('init', () => {
    it('restores a previously chosen preference', () => {
      localStorage.setItem('fleet_rental_theme', 'dark');

      store.init();

      expect(store.preference()).toBe('dark');
      expect(document.documentElement.classList.contains(DARK_CLASS)).toBe(true);
    });

    it('defaults to system when nothing was stored', () => {
      store.init();
      expect(store.preference()).toBe('system');
    });
  });

  describe('cycle', () => {
    it('goes light -> dark -> system -> light', () => {
      store.setPreference('light');

      store.cycle();
      expect(store.preference()).toBe('dark');

      store.cycle();
      expect(store.preference()).toBe('system');

      store.cycle();
      expect(store.preference()).toBe('light');
    });
  });
});
