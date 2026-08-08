import { Injectable, computed, inject, signal } from '@angular/core';

import { ApiService } from '../services/api.service';
import type {
  CarMaintenanceSummary,
  CreateServiceTypeRequest,
  IssueStatus,
  LogServiceRequest,
  ReportIssueRequest,
  ServiceRecord,
  ServiceType,
  ServiceTypeStatus,
  UpdateServiceTypeRequest,
  VehicleIssue,
} from '../models';

/**
 * Vehicle maintenance state for the owner app: service history, odometer and
 * service-interval tracking, and mechanical issue reporting (feature — owner
 * insights into car condition).
 */
@Injectable({ providedIn: 'root' })
export class MaintenanceStore {
  private readonly api = inject(ApiService);

  private readonly _summary = signal<CarMaintenanceSummary | null>(null);
  private readonly _history = signal<ServiceRecord[]>([]);
  private readonly _issues = signal<VehicleIssue[]>([]);
  private readonly _serviceTypes = signal<ServiceType[]>([]);
  private readonly _serviceTypeStatuses = signal<ServiceTypeStatus[]>([]);
  private readonly _loading = signal(false);
  private readonly _submitting = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly summary = this._summary.asReadonly();
  readonly history = this._history.asReadonly();
  readonly issues = this._issues.asReadonly();
  readonly serviceTypes = this._serviceTypes.asReadonly();
  readonly serviceTypeStatuses = this._serviceTypeStatuses.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly submitting = this._submitting.asReadonly();
  readonly error = this._error.asReadonly();

  /** Badge count for the admin nav — open issues across the whole fleet. */
  readonly openIssueCount = computed(
    () => this._issues().filter((i) => i.status !== 'Resolved').length,
  );

  readonly hasCriticalIssue = computed(() =>
    this._issues().some((i) => i.status !== 'Resolved' && i.severity === 'Critical'),
  );

  async loadSummary(carId: string): Promise<void> {
    this._error.set(null);

    try {
      this._summary.set(await this.api.getMaintenanceSummary(carId));
    } catch (error) {
      this._error.set((error as Error).message);
    }
  }

  async loadHistory(carId: string): Promise<void> {
    this._loading.set(true);
    this._error.set(null);

    try {
      this._history.set(await this.api.getServiceHistory(carId));
    } catch (error) {
      this._error.set((error as Error).message);
    } finally {
      this._loading.set(false);
    }
  }

  /** Fleet-wide by default, or scoped to one car — this is the owner's issue dashboard. */
  async loadIssues(carId?: string, status?: IssueStatus): Promise<void> {
    this._loading.set(true);
    this._error.set(null);

    try {
      this._issues.set(await this.api.getIssues(carId, status));
    } catch (error) {
      this._error.set((error as Error).message);
    } finally {
      this._loading.set(false);
    }
  }

  async logService(carId: string, request: LogServiceRequest): Promise<void> {
    this._submitting.set(true);
    this._error.set(null);

    try {
      const record = await this.api.logService(carId, request);
      this._history.update((list) => [record, ...list]);
      await this.loadSummary(carId); // odometer/due status may have changed
    } catch (error) {
      this._error.set((error as Error).message);
      throw error;
    } finally {
      this._submitting.set(false);
    }
  }

  async updateOdometer(carId: string, km: number): Promise<void> {
    this._submitting.set(true);
    this._error.set(null);

    try {
      await this.api.updateOdometer(carId, km);
      await this.loadSummary(carId);
    } catch (error) {
      this._error.set((error as Error).message);
      throw error;
    } finally {
      this._submitting.set(false);
    }
  }

  async setServiceInterval(carId: string, km: number | null): Promise<void> {
    this._submitting.set(true);
    this._error.set(null);

    try {
      await this.api.setServiceInterval(carId, km);
      await this.loadSummary(carId);
    } catch (error) {
      this._error.set((error as Error).message);
      throw error;
    } finally {
      this._submitting.set(false);
    }
  }

  /** Fleet-wide service catalog (feature: named services with per-service odometer tracking). */

  async loadServiceTypes(includeInactive = false): Promise<void> {
    this._loading.set(true);
    this._error.set(null);

    try {
      this._serviceTypes.set(await this.api.getServiceTypes(includeInactive));
    } catch (error) {
      this._error.set((error as Error).message);
    } finally {
      this._loading.set(false);
    }
  }

  async createServiceType(request: CreateServiceTypeRequest): Promise<void> {
    this._submitting.set(true);
    this._error.set(null);

    try {
      const created = await this.api.createServiceType(request);
      this._serviceTypes.update((list) => [...list, created]);
    } catch (error) {
      this._error.set((error as Error).message);
      throw error;
    } finally {
      this._submitting.set(false);
    }
  }

  async updateServiceType(id: string, request: UpdateServiceTypeRequest): Promise<void> {
    this._submitting.set(true);
    this._error.set(null);

    try {
      const updated = await this.api.updateServiceType(id, request);
      this._serviceTypes.update((list) => list.map((t) => (t.id === id ? updated : t)));
    } catch (error) {
      this._error.set((error as Error).message);
      throw error;
    } finally {
      this._submitting.set(false);
    }
  }

  async deactivateServiceType(id: string): Promise<void> {
    await this.setServiceTypeActive(id, () => this.api.deactivateServiceType(id));
  }

  async reactivateServiceType(id: string): Promise<void> {
    await this.setServiceTypeActive(id, () => this.api.reactivateServiceType(id));
  }

  async loadServiceTypeStatuses(carId: string): Promise<void> {
    this._error.set(null);

    try {
      this._serviceTypeStatuses.set(await this.api.getServiceTypeStatuses(carId));
    } catch (error) {
      this._error.set((error as Error).message);
    }
  }

  private async setServiceTypeActive(id: string, operation: () => Promise<ServiceType>): Promise<void> {
    this._submitting.set(true);
    this._error.set(null);

    try {
      const updated = await operation();
      this._serviceTypes.update((list) => list.map((t) => (t.id === id ? updated : t)));
    } catch (error) {
      this._error.set((error as Error).message);
      throw error;
    } finally {
      this._submitting.set(false);
    }
  }

  async reportIssue(carId: string, request: ReportIssueRequest): Promise<void> {
    this._submitting.set(true);
    this._error.set(null);

    try {
      const issue = await this.api.reportIssue(carId, request);
      this._issues.update((list) => [issue, ...list]);
      await this.loadSummary(carId);
    } catch (error) {
      this._error.set((error as Error).message);
      throw error;
    } finally {
      this._submitting.set(false);
    }
  }

  async startProgress(issueId: string): Promise<void> {
    await this.mutateIssue(() => this.api.startIssueProgress(issueId));
  }

  async resolve(issueId: string, resolutionNotes?: string): Promise<void> {
    await this.mutateIssue(() => this.api.resolveIssue(issueId, resolutionNotes));
  }

  async reopen(issueId: string): Promise<void> {
    await this.mutateIssue(() => this.api.reopenIssue(issueId));
  }

  clearError(): void {
    this._error.set(null);
  }

  private async mutateIssue(operation: () => Promise<VehicleIssue>): Promise<void> {
    this._submitting.set(true);
    this._error.set(null);

    try {
      const updated = await operation();
      this._issues.update((list) => list.map((i) => (i.id === updated.id ? updated : i)));

      const current = this._summary();
      if (current?.carId === updated.carId) {
        await this.loadSummary(updated.carId);
      }
    } catch (error) {
      this._error.set((error as Error).message);
      throw error;
    } finally {
      this._submitting.set(false);
    }
  }
}
