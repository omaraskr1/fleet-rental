import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { tenantInterceptor } from './tenant.interceptor';
import { TenantStore } from '../stores/tenant.store';
import { environment } from '../../../environments/environment';

describe('tenantInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let tenant: TenantStore;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([tenantInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    tenant = TestBed.inject(TenantStore);
  });

  afterEach(() => httpMock.verify());

  it('attaches no header before any company has been selected', () => {
    http.get('/api/cars').subscribe();

    const req = httpMock.expectOne('/api/cars');
    expect(req.request.headers.has('X-Tenant-Code')).toBe(false);
    req.flush([]);
  });

  it('attaches the selected company code once one has been chosen', () => {
    localStorage.setItem(
      'fleet_rental_tenant',
      JSON.stringify({ code: 'gulf-fleet', name: 'Gulf Fleet' }),
    );
    tenant.restore();

    http.get('/api/cars').subscribe();

    const req = httpMock.expectOne('/api/cars');
    expect(req.request.headers.get('X-Tenant-Code')).toBe('gulf-fleet');
    req.flush([]);
  });

  it('never attaches the header to the tenant-lookup call itself', () => {
    localStorage.setItem(
      'fleet_rental_tenant',
      JSON.stringify({ code: 'gulf-fleet', name: 'Gulf Fleet' }),
    );
    tenant.restore();

    // This is the request that establishes which tenant to use in the first
    // place; scoping it to a tenant would make it unable to look up a different one.
    http.get(`${environment.apiBaseUrl}/tenants/other-company`).subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/tenants/other-company`);
    expect(req.request.headers.has('X-Tenant-Code')).toBe(false);
    req.flush({ code: 'other-company', name: 'Other' });
  });
});
