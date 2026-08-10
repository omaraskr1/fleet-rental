import { TestBed } from '@angular/core/testing';

import { PlatformCompaniesStore } from './platform-companies.store';
import { ApiService } from '../services/api.service';
import type { Company, CompanyAdmin } from '../models';

function company(overrides: Partial<Company> = {}): Company {
  return { id: 'c1', name: 'Test Co', code: 'test-co', contactEmail: null, status: 'Active', ...overrides };
}

describe('PlatformCompaniesStore', () => {
  let api: {
    getCompanies: ReturnType<typeof vi.fn>;
    createCompany: ReturnType<typeof vi.fn>;
    suspendCompany: ReturnType<typeof vi.fn>;
    reactivateCompany: ReturnType<typeof vi.fn>;
    getCompanyAdmins: ReturnType<typeof vi.fn>;
    createCompanyAdmin: ReturnType<typeof vi.fn>;
  };
  let store: PlatformCompaniesStore;

  beforeEach(() => {
    api = {
      getCompanies: vi.fn(),
      createCompany: vi.fn(),
      suspendCompany: vi.fn(),
      reactivateCompany: vi.fn(),
      getCompanyAdmins: vi.fn(),
      createCompanyAdmin: vi.fn(),
    };
    TestBed.configureTestingModule({ providers: [{ provide: ApiService, useValue: api }] });
    store = TestBed.inject(PlatformCompaniesStore);
  });

  it('starts empty', () => {
    expect(store.companies()).toEqual([]);
    expect(store.admins()).toEqual([]);
    expect(store.loading()).toBe(false);
  });

  it('loadCompanies populates the list', async () => {
    api.getCompanies.mockResolvedValue([company({ id: 'c1' }), company({ id: 'c2' })]);

    await store.loadCompanies();

    expect(store.companies()).toHaveLength(2);
    expect(store.loading()).toBe(false);
  });

  it('a failed loadCompanies surfaces the error and clears loading', async () => {
    api.getCompanies.mockRejectedValue(new Error('Cannot reach the server.'));

    await store.loadCompanies();

    expect(store.error()).toBe('Cannot reach the server.');
    expect(store.loading()).toBe(false);
  });

  it('createCompany reloads the list so the new company appears without a manual refresh', async () => {
    api.createCompany.mockResolvedValue(company({ id: 'new-co' }));
    api.getCompanies.mockResolvedValue([company({ id: 'new-co' })]);

    const created = await store.createCompany({ name: 'New Co', code: 'new-co' });

    expect(created.id).toBe('new-co');
    expect(api.getCompanies).toHaveBeenCalled();
    expect(store.companies()).toHaveLength(1);
    expect(store.submitting()).toBe(false);
  });

  it('a failed createCompany surfaces the error, rethrows, and does not reload', async () => {
    api.createCompany.mockRejectedValue(new Error('Code already in use.'));

    await expect(store.createCompany({ name: 'Dup', code: 'dup' })).rejects.toThrow('Code already in use.');

    expect(store.error()).toBe('Code already in use.');
    expect(api.getCompanies).not.toHaveBeenCalled();
    expect(store.submitting()).toBe(false);
  });

  it('suspendCompany reloads so the status flip is reflected immediately', async () => {
    api.suspendCompany.mockResolvedValue(company({ status: 'Suspended' }));
    api.getCompanies.mockResolvedValue([company({ status: 'Suspended' })]);

    await store.suspendCompany('c1');

    expect(api.suspendCompany).toHaveBeenCalledWith('c1');
    expect(store.companies()[0].status).toBe('Suspended');
  });

  it('reactivateCompany reloads so the status flip is reflected immediately', async () => {
    api.reactivateCompany.mockResolvedValue(company({ status: 'Active' }));
    api.getCompanies.mockResolvedValue([company({ status: 'Active' })]);

    await store.reactivateCompany('c1');

    expect(api.reactivateCompany).toHaveBeenCalledWith('c1');
    expect(store.companies()[0].status).toBe('Active');
  });

  it('loadAdmins populates the admin list for the given company', async () => {
    const admins: CompanyAdmin[] = [{ id: 'a1', email: 'owner@co.com', fullName: 'Owner', isActive: true }];
    api.getCompanyAdmins.mockResolvedValue(admins);

    await store.loadAdmins('c1');

    expect(api.getCompanyAdmins).toHaveBeenCalledWith('c1');
    expect(store.admins()).toEqual(admins);
  });

  it('createAdmin reloads the admin list for that company', async () => {
    const created: CompanyAdmin = { id: 'a2', email: 'new@co.com', fullName: 'New Admin', isActive: true };
    api.createCompanyAdmin.mockResolvedValue(created);
    api.getCompanyAdmins.mockResolvedValue([created]);

    const result = await store.createAdmin('c1', { email: 'new@co.com', password: 'pw', fullName: 'New Admin' });

    expect(result).toEqual(created);
    expect(api.getCompanyAdmins).toHaveBeenCalledWith('c1');
    expect(store.admins()).toEqual([created]);
  });

  it('a failed createAdmin surfaces the error and rethrows without touching the admin list', async () => {
    api.createCompanyAdmin.mockRejectedValue(new Error('Email already registered.'));

    await expect(
      store.createAdmin('c1', { email: 'dup@co.com', password: 'pw', fullName: 'Dup' }),
    ).rejects.toThrow('Email already registered.');

    expect(store.error()).toBe('Email already registered.');
    expect(store.admins()).toEqual([]);
  });

  it('clearAdmins empties the admin list without touching companies', async () => {
    api.getCompanyAdmins.mockResolvedValue([{ id: 'a1', email: 'x@co.com', fullName: 'X', isActive: true }]);
    await store.loadAdmins('c1');
    expect(store.admins()).toHaveLength(1);

    store.clearAdmins();

    expect(store.admins()).toEqual([]);
  });
});
