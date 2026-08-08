import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { LocaleStore } from './locale.store';

const EN = {
  common: { appName: 'Fleet Rental' },
  tenant: { notFound: 'We couldn\'t find a company with the code "{{code}}".' },
};

const AR = {
  common: { appName: 'فليت رنتال' },
  tenant: { notFound: 'لم نتمكن من العثور على شركة بالرمز "{{code}}".' },
};

describe('LocaleStore', () => {
  let store: LocaleStore;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('lang');
    document.documentElement.removeAttribute('dir');

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    store = TestBed.inject(LocaleStore);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('defaults to English before init', () => {
    expect(store.locale()).toBe('en');
    expect(store.isRtl()).toBe(false);
  });

  describe('init', () => {
    it('loads English by default when nothing was stored', async () => {
      const init = store.init();
      httpMock.expectOne('/i18n/en.json').flush(EN);
      await init;

      expect(store.locale()).toBe('en');
      expect(store.t('common.appName')).toBe('Fleet Rental');
    });

    it('restores a previously chosen Arabic locale and sets RTL', async () => {
      localStorage.setItem('fleet_rental_locale', 'ar');

      const init = store.init();
      httpMock.expectOne('/i18n/ar.json').flush(AR);
      await init;

      expect(store.locale()).toBe('ar');
      expect(store.isRtl()).toBe(true);
      expect(document.documentElement.dir).toBe('rtl');
      expect(document.documentElement.lang).toBe('ar');
    });

    it('an unrecognised stored value falls back to English rather than throwing', async () => {
      localStorage.setItem('fleet_rental_locale', 'fr');

      const init = store.init();
      httpMock.expectOne('/i18n/en.json').flush(EN);
      await init;

      expect(store.locale()).toBe('en');
    });
  });

  describe('setLocale', () => {
    it('sets dir=ltr and lang=en for English', async () => {
      await runSetLocale(store, httpMock, 'en', EN);

      expect(document.documentElement.dir).toBe('ltr');
      expect(document.documentElement.lang).toBe('en');
    });

    it('caches translations so switching back to a loaded language does not refetch', async () => {
      await runSetLocale(store, httpMock, 'en', EN);
      await runSetLocale(store, httpMock, 'ar', AR);

      const backToEnglish = store.setLocale('en');
      httpMock.expectNone('/i18n/en.json');
      await backToEnglish;

      expect(store.locale()).toBe('en');
    });

    it('persists the choice for the next launch', async () => {
      await runSetLocale(store, httpMock, 'ar', AR);
      expect(localStorage.getItem('fleet_rental_locale')).toBe('ar');
    });
  });

  describe('toggle', () => {
    it('flips between en and ar', async () => {
      await runSetLocale(store, httpMock, 'en', EN);

      const toggled = store.toggle();
      httpMock.expectOne('/i18n/ar.json').flush(AR);
      await toggled;

      expect(store.locale()).toBe('ar');
    });
  });

  describe('t', () => {
    it('resolves a nested dot-path key', async () => {
      await runSetLocale(store, httpMock, 'en', EN);
      expect(store.t('common.appName')).toBe('Fleet Rental');
    });

    it('substitutes {{placeholder}} tokens', async () => {
      await runSetLocale(store, httpMock, 'en', EN);
      expect(store.t('tenant.notFound', { code: 'acme' })).toBe(
        'We couldn\'t find a company with the code "acme".',
      );
    });

    it('returns the key itself for a missing translation, so a gap is visible rather than blank', async () => {
      await runSetLocale(store, httpMock, 'en', EN);
      expect(store.t('nothing.here')).toBe('nothing.here');
    });
  });
});

async function runSetLocale(
  store: LocaleStore,
  httpMock: HttpTestingController,
  locale: 'en' | 'ar',
  body: object,
): Promise<void> {
  const promise = store.setLocale(locale);
  httpMock.expectOne(`/i18n/${locale}.json`).flush(body);
  await promise;
}
