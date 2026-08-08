import { TestBed } from '@angular/core/testing';

import { MaintenanceStore } from './maintenance.store';
import { ApiService } from '../services/api.service';
import type { CarMaintenanceSummary, ServiceRecord, ServiceType, VehicleIssue } from '../models';

function summary(overrides: Partial<CarMaintenanceSummary> = {}): CarMaintenanceSummary {
  return {
    carId: 'c1',
    carName: 'Test Car',
    currentOdometerKm: 30_000,
    serviceIntervalKm: 10_000,
    lastServiceAt: '2026-01-01',
    kmSinceLastService: 5_000,
    isServiceDue: false,
    openIssueCount: 0,
    hasBlockingIssue: false,
    ...overrides,
  };
}

function record(overrides: Partial<ServiceRecord> = {}): ServiceRecord {
  return {
    id: 's1',
    carId: 'c1',
    performedAt: '2026-01-01',
    description: 'Oil change',
    odometerKm: 30_000,
    cost: 200,
    performedBy: 'Test Garage',
    serviceTypeId: null,
    serviceTypeName: null,
    ...overrides,
  };
}

function serviceType(overrides: Partial<ServiceType> = {}): ServiceType {
  return {
    id: 'st1',
    name: 'Oil change',
    intervalKm: 10_000,
    isActive: true,
    ...overrides,
  };
}

function issue(overrides: Partial<VehicleIssue> = {}): VehicleIssue {
  return {
    id: 'i1',
    carId: 'c1',
    carName: 'Test Car',
    reportedByName: 'Test Admin',
    description: 'AC not cooling',
    severity: 'Medium',
    status: 'Open',
    reportedAt: '2026-01-01T00:00:00Z',
    resolvedAt: null,
    resolutionNotes: null,
    ...overrides,
  };
}

describe('MaintenanceStore', () => {
  let api: {
    getMaintenanceSummary: ReturnType<typeof vi.fn>;
    getServiceHistory: ReturnType<typeof vi.fn>;
    logService: ReturnType<typeof vi.fn>;
    updateOdometer: ReturnType<typeof vi.fn>;
    setServiceInterval: ReturnType<typeof vi.fn>;
    reportIssue: ReturnType<typeof vi.fn>;
    getIssues: ReturnType<typeof vi.fn>;
    startIssueProgress: ReturnType<typeof vi.fn>;
    resolveIssue: ReturnType<typeof vi.fn>;
    reopenIssue: ReturnType<typeof vi.fn>;
    getServiceTypes: ReturnType<typeof vi.fn>;
    createServiceType: ReturnType<typeof vi.fn>;
    updateServiceType: ReturnType<typeof vi.fn>;
    deactivateServiceType: ReturnType<typeof vi.fn>;
    reactivateServiceType: ReturnType<typeof vi.fn>;
    getServiceTypeStatuses: ReturnType<typeof vi.fn>;
  };
  let store: MaintenanceStore;

  beforeEach(() => {
    api = {
      getMaintenanceSummary: vi.fn(),
      getServiceHistory: vi.fn(),
      logService: vi.fn(),
      updateOdometer: vi.fn(),
      setServiceInterval: vi.fn(),
      reportIssue: vi.fn(),
      getIssues: vi.fn(),
      startIssueProgress: vi.fn(),
      resolveIssue: vi.fn(),
      reopenIssue: vi.fn(),
      getServiceTypes: vi.fn(),
      createServiceType: vi.fn(),
      updateServiceType: vi.fn(),
      deactivateServiceType: vi.fn(),
      reactivateServiceType: vi.fn(),
      getServiceTypeStatuses: vi.fn(),
    };

    TestBed.configureTestingModule({ providers: [{ provide: ApiService, useValue: api }] });
    store = TestBed.inject(MaintenanceStore);
  });

  describe('loadSummary', () => {
    it('populates the summary signal', async () => {
      api.getMaintenanceSummary.mockResolvedValue(summary());
      await store.loadSummary('c1');
      expect(store.summary()).toEqual(summary());
    });

    it('surfaces a failure without throwing', async () => {
      api.getMaintenanceSummary.mockRejectedValue(new Error('not found'));
      await store.loadSummary('c1');
      expect(store.error()).toBe('not found');
      expect(store.summary()).toBeNull();
    });
  });

  describe('loadHistory', () => {
    it('populates the history list', async () => {
      api.getServiceHistory.mockResolvedValue([record()]);
      await store.loadHistory('c1');
      expect(store.history()).toEqual([record()]);
    });
  });

  describe('loadIssues', () => {
    it('populates the issues list', async () => {
      api.getIssues.mockResolvedValue([issue()]);
      await store.loadIssues();
      expect(store.issues()).toEqual([issue()]);
    });

    it('forwards the carId and status filters to the API', async () => {
      api.getIssues.mockResolvedValue([]);
      await store.loadIssues('c1', 'Open');
      expect(api.getIssues).toHaveBeenCalledWith('c1', 'Open');
    });
  });

  describe('openIssueCount and hasCriticalIssue', () => {
    it('counts everything that is not Resolved', async () => {
      api.getIssues.mockResolvedValue([
        issue({ id: 'i1', status: 'Open' }),
        issue({ id: 'i2', status: 'InProgress' }),
        issue({ id: 'i3', status: 'Resolved' }),
      ]);

      await store.loadIssues();

      expect(store.openIssueCount()).toBe(2);
    });

    it('is true only when an unresolved issue is Critical severity', async () => {
      api.getIssues.mockResolvedValue([
        issue({ id: 'i1', severity: 'Critical', status: 'Resolved' }),
        issue({ id: 'i2', severity: 'High', status: 'Open' }),
      ]);
      await store.loadIssues();
      expect(store.hasCriticalIssue()).toBe(false);

      api.getIssues.mockResolvedValue([issue({ id: 'i3', severity: 'Critical', status: 'Open' })]);
      await store.loadIssues();
      expect(store.hasCriticalIssue()).toBe(true);
    });
  });

  describe('logService', () => {
    it('prepends the new record and refreshes the summary', async () => {
      const created = record({ id: 'new-1' });
      api.logService.mockResolvedValue(created);
      api.getMaintenanceSummary.mockResolvedValue(summary());
      api.getServiceHistory.mockResolvedValue([record({ id: 'existing' })]);
      await store.loadHistory('c1');

      await store.logService('c1', {
        performedAt: '2026-02-01',
        description: 'Brake pads',
        odometerKm: 35_000,
        cost: 300,
      });

      expect(store.history().map((r) => r.id)).toEqual(['new-1', 'existing']);
      expect(api.getMaintenanceSummary).toHaveBeenCalledWith('c1');
    });

    it('rethrows on failure and records the message', async () => {
      api.logService.mockRejectedValue(new Error('Service date cannot be in the future.'));

      await expect(
        store.logService('c1', { performedAt: '2099-01-01', description: 'x', odometerKm: null, cost: 0 }),
      ).rejects.toThrow('future');

      expect(store.error()).toBe('Service date cannot be in the future.');
      expect(store.submitting()).toBe(false);
    });
  });

  describe('updateOdometer / setServiceInterval', () => {
    it('updateOdometer refreshes the summary afterwards', async () => {
      api.updateOdometer.mockResolvedValue(undefined);
      api.getMaintenanceSummary.mockResolvedValue(summary({ currentOdometerKm: 40_000 }));

      await store.updateOdometer('c1', 40_000);

      expect(api.updateOdometer).toHaveBeenCalledWith('c1', 40_000);
      expect(store.summary()?.currentOdometerKm).toBe(40_000);
    });

    it('setServiceInterval refreshes the summary afterwards', async () => {
      api.setServiceInterval.mockResolvedValue(undefined);
      api.getMaintenanceSummary.mockResolvedValue(summary({ serviceIntervalKm: 15_000 }));

      await store.setServiceInterval('c1', 15_000);

      expect(api.setServiceInterval).toHaveBeenCalledWith('c1', 15_000);
      expect(store.summary()?.serviceIntervalKm).toBe(15_000);
    });
  });

  describe('reportIssue', () => {
    it('prepends the new issue and refreshes the summary', async () => {
      const created = issue({ id: 'new-issue' });
      api.reportIssue.mockResolvedValue(created);
      api.getMaintenanceSummary.mockResolvedValue(summary({ openIssueCount: 1 }));
      api.getIssues.mockResolvedValue([issue({ id: 'existing' })]);
      await store.loadIssues();

      await store.reportIssue('c1', { description: 'Rattling noise', severity: 'Low' });

      expect(store.issues().map((i) => i.id)).toEqual(['new-issue', 'existing']);
    });
  });

  describe('issue lifecycle mutations', () => {
    async function seedOneIssue(status: VehicleIssue['status'] = 'Open') {
      api.getIssues.mockResolvedValue([issue({ id: 'i1', status })]);
      await store.loadIssues();
    }

    it('startProgress replaces the issue with the servers response', async () => {
      await seedOneIssue('Open');
      api.startIssueProgress.mockResolvedValue(issue({ id: 'i1', status: 'InProgress' }));

      await store.startProgress('i1');

      expect(store.issues()[0].status).toBe('InProgress');
    });

    it('resolve replaces the issue and, if it belongs to the loaded summary car, refreshes the summary', async () => {
      await seedOneIssue('Open');
      api.getMaintenanceSummary.mockResolvedValue(summary({ carId: 'c1' }));
      await store.loadSummary('c1');

      api.resolveIssue.mockResolvedValue(issue({ id: 'i1', carId: 'c1', status: 'Resolved' }));
      api.getMaintenanceSummary.mockResolvedValue(summary({ carId: 'c1', openIssueCount: 0 }));

      await store.resolve('i1', 'Fixed the AC');

      expect(store.issues()[0].status).toBe('Resolved');
      expect(api.resolveIssue).toHaveBeenCalledWith('i1', 'Fixed the AC');
      expect(store.summary()?.openIssueCount).toBe(0);
    });

    it('reopen replaces the issue back to Open', async () => {
      await seedOneIssue('Resolved');
      api.reopenIssue.mockResolvedValue(issue({ id: 'i1', status: 'Open' }));

      await store.reopen('i1');

      expect(store.issues()[0].status).toBe('Open');
    });

    it('a failed mutation rethrows and records the error', async () => {
      await seedOneIssue('Open');
      api.startIssueProgress.mockRejectedValue(new Error('Only an open issue can move to in-progress.'));

      await expect(store.startProgress('i1')).rejects.toThrow('in-progress');
      expect(store.error()).toBe('Only an open issue can move to in-progress.');
    });
  });

  describe('service catalog', () => {
    it('loadServiceTypes populates the list', async () => {
      api.getServiceTypes.mockResolvedValue([serviceType()]);
      await store.loadServiceTypes();
      expect(store.serviceTypes()).toEqual([serviceType()]);
    });

    it('createServiceType appends the new type', async () => {
      api.getServiceTypes.mockResolvedValue([serviceType({ id: 'existing' })]);
      await store.loadServiceTypes();

      const created = serviceType({ id: 'new-type', name: 'Tire rotation', intervalKm: 8_000 });
      api.createServiceType.mockResolvedValue(created);

      await store.createServiceType({ name: 'Tire rotation', intervalKm: 8_000 });

      expect(store.serviceTypes().map((t) => t.id)).toEqual(['existing', 'new-type']);
    });

    it('updateServiceType replaces the matching entry', async () => {
      api.getServiceTypes.mockResolvedValue([serviceType({ id: 'st1', intervalKm: 10_000 })]);
      await store.loadServiceTypes();

      api.updateServiceType.mockResolvedValue(serviceType({ id: 'st1', intervalKm: 12_000 }));
      await store.updateServiceType('st1', { name: 'Oil change', intervalKm: 12_000 });

      expect(store.serviceTypes()[0].intervalKm).toBe(12_000);
    });

    it('deactivateServiceType and reactivateServiceType update the entry in place', async () => {
      api.getServiceTypes.mockResolvedValue([serviceType({ id: 'st1', isActive: true })]);
      await store.loadServiceTypes();

      api.deactivateServiceType.mockResolvedValue(serviceType({ id: 'st1', isActive: false }));
      await store.deactivateServiceType('st1');
      expect(store.serviceTypes()[0].isActive).toBe(false);

      api.reactivateServiceType.mockResolvedValue(serviceType({ id: 'st1', isActive: true }));
      await store.reactivateServiceType('st1');
      expect(store.serviceTypes()[0].isActive).toBe(true);
    });

    it('loadServiceTypeStatuses populates the per-car breakdown', async () => {
      const status = {
        serviceTypeId: 'st1',
        serviceTypeName: 'Oil change',
        intervalKm: 10_000,
        lastPerformedAt: '2026-01-01',
        kmSinceLastService: 5_000,
        isDue: false,
      };
      api.getServiceTypeStatuses.mockResolvedValue([status]);

      await store.loadServiceTypeStatuses('c1');

      expect(store.serviceTypeStatuses()).toEqual([status]);
    });

    it('a failed mutation rethrows and records the error', async () => {
      api.createServiceType.mockRejectedValue(new Error('Service name is required.'));

      await expect(store.createServiceType({ name: '', intervalKm: 10_000 })).rejects.toThrow(
        'Service name is required.',
      );
      expect(store.error()).toBe('Service name is required.');
    });
  });

  it('clearError resets only the error signal', async () => {
    api.logService.mockRejectedValue(new Error('boom'));
    await expect(
      store.logService('c1', { performedAt: '2026-01-01', description: 'x', odometerKm: null, cost: 0 }),
    ).rejects.toThrow();

    store.clearError();

    expect(store.error()).toBeNull();
  });
});
