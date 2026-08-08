import { TestBed } from '@angular/core/testing';

import { PlatformCarsStore } from './platform-cars.store';
import { ApiService } from '../services/api.service';
import type { CreatePlatformCarRequest, PlatformCar } from '../models';

function car(overrides: Partial<PlatformCar> = {}): PlatformCar {
  return {
    id: 'c1',
    companyId: 't1',
    companyName: 'Alpha Fleet',
    name: 'Test Car',
    description: '',
    category: 'Sedan',
    seats: 4,
    rate: 100,
    pricingModel: 'PerDay',
    status: 'Active',
    licensePlate: null,
    ...overrides,
  };
}

describe('PlatformCarsStore', () => {
  let api: {
    getPlatformCars: ReturnType<typeof vi.fn>;
    createPlatformCar: ReturnType<typeof vi.fn>;
    updatePlatformCar: ReturnType<typeof vi.fn>;
    retirePlatformCar: ReturnType<typeof vi.fn>;
  };
  let store: PlatformCarsStore;

  beforeEach(() => {
    api = {
      getPlatformCars: vi.fn(),
      createPlatformCar: vi.fn(),
      updatePlatformCar: vi.fn(),
      retirePlatformCar: vi.fn(),
    };
    TestBed.configureTestingModule({ providers: [{ provide: ApiService, useValue: api }] });
    store = TestBed.inject(PlatformCarsStore);
  });

  it('loadCars populates the cross-company list', async () => {
    const cars = [car({ id: 'c1', companyName: 'Alpha' }), car({ id: 'c2', companyName: 'Beta' })];
    api.getPlatformCars.mockResolvedValue(cars);

    await store.loadCars();

    expect(store.cars()).toEqual(cars);
    expect(store.loading()).toBe(false);
  });

  it('a failed load surfaces the message', async () => {
    api.getPlatformCars.mockRejectedValue(new Error('Cannot reach the server.'));

    await store.loadCars();

    expect(store.error()).toBe('Cannot reach the server.');
    expect(store.cars()).toEqual([]);
  });

  it('createCar targets the chosen company and reloads the list', async () => {
    const request: CreatePlatformCarRequest = {
      companyId: 't1',
      name: 'New Car',
      description: '',
      category: 'Sedan',
      seats: 4,
      rate: 150,
      pricingModel: 'PerDay',
    };
    api.createPlatformCar.mockResolvedValue(car({ id: 'new-id' }));
    api.getPlatformCars.mockResolvedValue([car({ id: 'new-id' })]);

    const result = await store.createCar(request);

    expect(api.createPlatformCar).toHaveBeenCalledWith(request);
    expect(result.id).toBe('new-id');
    expect(store.cars().map((c) => c.id)).toEqual(['new-id']);
  });

  it('createCar surfaces the error and rethrows without reloading', async () => {
    api.createPlatformCar.mockRejectedValue(new Error('Name is required.'));
    api.getPlatformCars.mockResolvedValue([]);
    await store.loadCars();

    await expect(
      store.createCar({
        companyId: 't1',
        name: '',
        description: '',
        category: 'Sedan',
        seats: 4,
        rate: 100,
        pricingModel: 'PerDay',
      }),
    ).rejects.toThrow('Name is required.');

    expect(store.error()).toBe('Name is required.');
    expect(api.getPlatformCars).toHaveBeenCalledTimes(1);
  });

  it('updateCar reloads the list after a successful edit', async () => {
    api.updatePlatformCar.mockResolvedValue(car({ id: 'c1', rate: 200 }));
    api.getPlatformCars.mockResolvedValue([car({ id: 'c1', rate: 200 })]);

    await store.updateCar('c1', {
      name: 'Renamed',
      description: '',
      category: 'Sedan',
      seats: 4,
      rate: 200,
      pricingModel: 'PerDay',
    });

    expect(store.cars()[0].rate).toBe(200);
  });

  it('retireCar reloads the list after retiring', async () => {
    api.retirePlatformCar.mockResolvedValue(undefined);
    api.getPlatformCars.mockResolvedValue([car({ id: 'c1', status: 'Retired' })]);

    await store.retireCar('c1');

    expect(api.retirePlatformCar).toHaveBeenCalledWith('c1');
    expect(store.cars()[0].status).toBe('Retired');
  });
});
