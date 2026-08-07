import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { TenantStore } from './tenant.store';
import { LocaleStore } from './locale.store';
import { environment } from '../../../environments/environment';

describe('TenantStore', () => {
  let store: TenantStore;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    store = TestBed.inject(TenantStore);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('starts with no company selected', () => {
    expect(store.isSelected()).toBe(false);
    expect(store.code()).toBeNull();
  });

  describe('selectByCode', () => {
    it('resolves the code, stores the tenant, and persists it for next launch', async () => {
      const promise = store.selectByCode('Gulf-Fleet');

      const req = httpMock.expectOne(`${environment.apiBaseUrl}/tenants/gulf-fleet`);
      expect(req.request.method).toBe('GET');
      req.flush({ code: 'gulf-fleet', name: 'Gulf Fleet' });

      const found = await promise;

      expect(found).toBe(true);
      expect(store.isSelected()).toBe(true);
      expect(store.code()).toBe('gulf-fleet');
      expect(store.name()).toBe('Gulf Fleet');
      expect(JSON.parse(localStorage.getItem('fleet_rental_tenant')!)).toEqual({
        code: 'gulf-fleet',
        name: 'Gulf Fleet',
      });
    });

    it('normalises the code to lowercase and trims whitespace before requesting it', async () => {
      const promise = store.selectByCode('  Gulf-Fleet  ');

      httpMock.expectOne(`${environment.apiBaseUrl}/tenants/gulf-fleet`).flush({
        code: 'gulf-fleet',
        name: 'Gulf Fleet',
      });

      await promise;
    });

    it('an empty or blank code is rejected before any request is made', async () => {
      const found = await store.selectByCode('   ');

      expect(found).toBe(false);
      httpMock.expectNone(() => true);
    });

    it('an unknown or suspended company yields the same generic message, never distinguishing the two', async () => {
      // The message is built via LocaleStore.t(), which returns the raw key
      // until real translations are loaded — so this test loads them first,
      // the same way app.ts does before the UI ever shows anything.
      const locale = TestBed.inject(LocaleStore);
      const localeInit = locale.init();
      httpMock.expectOne('/i18n/en.json').flush({
        tenant: { notFound: 'We couldn\'t find a company with the code "{{code}}".' },
      });
      await localeInit;

      const promise = store.selectByCode('no-such-company');

      httpMock.expectOne(`${environment.apiBaseUrl}/tenants/no-such-company`).flush(
        { status: 404, title: 'Not Found', detail: 'Not found' },
        { status: 404, statusText: 'Not Found' },
      );

      const found = await promise;

      expect(found).toBe(false);
      expect(store.isSelected()).toBe(false);
      expect(store.error()).toContain('no-such-company');
    });

    it('loading is true only while the lookup is in flight', async () => {
      const promise = store.selectByCode('gulf-fleet');

      expect(store.loading()).toBe(true);

      httpMock.expectOne(`${environment.apiBaseUrl}/tenants/gulf-fleet`).flush({
        code: 'gulf-fleet',
        name: 'Gulf Fleet',
      });
      await promise;

      expect(store.loading()).toBe(false);
    });
  });

  describe('restore', () => {
    it('reads a previously selected company back on a fresh instance', () => {
      localStorage.setItem(
        'fleet_rental_tenant',
        JSON.stringify({ code: 'gulf-fleet', name: 'Gulf Fleet' }),
      );

      store.restore();

      expect(store.isSelected()).toBe(true);
      expect(store.code()).toBe('gulf-fleet');
    });

    it('does nothing when nothing was stored', () => {
      store.restore();
      expect(store.isSelected()).toBe(false);
    });

    it('discards a corrupted stored value rather than throwing', () => {
      localStorage.setItem('fleet_rental_tenant', '{not valid json');

      expect(() => store.restore()).not.toThrow();
      expect(store.isSelected()).toBe(false);
      expect(localStorage.getItem('fleet_rental_tenant')).toBeNull();
    });
  });

  describe('clear', () => {
    it('removes the selection from both the signal and storage', async () => {
      const promise = store.selectByCode('gulf-fleet');
      httpMock.expectOne(`${environment.apiBaseUrl}/tenants/gulf-fleet`).flush({
        code: 'gulf-fleet',
        name: 'Gulf Fleet',
      });
      await promise;

      store.clear();

      expect(store.isSelected()).toBe(false);
      expect(localStorage.getItem('fleet_rental_tenant')).toBeNull();
    });
  });
});
