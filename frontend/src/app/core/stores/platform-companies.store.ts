import { Injectable, inject, signal } from '@angular/core';

import { ApiService } from '../services/api.service';
import type { Company, CompanyAdmin, CreateCompanyAdminRequest, CreateCompanyRequest } from '../models';

/** Company (tenant) management for the platform panel — list, create, suspend, and per-company admins. */
@Injectable({ providedIn: 'root' })
export class PlatformCompaniesStore {
  private readonly api = inject(ApiService);

  private readonly _companies = signal<Company[]>([]);
  private readonly _admins = signal<CompanyAdmin[]>([]);
  private readonly _loading = signal(false);
  private readonly _submitting = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly companies = this._companies.asReadonly();
  readonly admins = this._admins.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly submitting = this._submitting.asReadonly();
  readonly error = this._error.asReadonly();

  async loadCompanies(): Promise<void> {
    this._loading.set(true);
    this._error.set(null);

    try {
      this._companies.set(await this.api.getCompanies());
    } catch (error) {
      this._error.set((error as Error).message);
    } finally {
      this._loading.set(false);
    }
  }

  async createCompany(request: CreateCompanyRequest): Promise<Company> {
    this._submitting.set(true);
    this._error.set(null);

    try {
      const created = await this.api.createCompany(request);
      await this.loadCompanies();
      return created;
    } catch (error) {
      this._error.set((error as Error).message);
      throw error;
    } finally {
      this._submitting.set(false);
    }
  }

  async suspendCompany(tenantId: string): Promise<void> {
    this._error.set(null);

    try {
      await this.api.suspendCompany(tenantId);
      await this.loadCompanies();
    } catch (error) {
      this._error.set((error as Error).message);
      throw error;
    }
  }

  async reactivateCompany(tenantId: string): Promise<void> {
    this._error.set(null);

    try {
      await this.api.reactivateCompany(tenantId);
      await this.loadCompanies();
    } catch (error) {
      this._error.set((error as Error).message);
      throw error;
    }
  }

  async loadAdmins(tenantId: string): Promise<void> {
    this._loading.set(true);
    this._error.set(null);

    try {
      this._admins.set(await this.api.getCompanyAdmins(tenantId));
    } catch (error) {
      this._error.set((error as Error).message);
    } finally {
      this._loading.set(false);
    }
  }

  async createAdmin(tenantId: string, request: CreateCompanyAdminRequest): Promise<CompanyAdmin> {
    this._submitting.set(true);
    this._error.set(null);

    try {
      const created = await this.api.createCompanyAdmin(tenantId, request);
      await this.loadAdmins(tenantId);
      return created;
    } catch (error) {
      this._error.set((error as Error).message);
      throw error;
    } finally {
      this._submitting.set(false);
    }
  }

  clearAdmins(): void {
    this._admins.set([]);
  }
}
