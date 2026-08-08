import { TestBed } from '@angular/core/testing';

import { CarsStore } from './cars.store';
import { ApiService } from '../services/api.service';
import type { CarAvailability, CarDetail, CarListItem, CreateCarRequest } from '../models';

function car(overrides: Partial<CarListItem> = {}): CarListItem {
  return {
    id: 'c1',
    name: 'Test Car',
    category: 'Sedan',
    seats: 4,
    rate: 100,
    pricingModel: 'PerDay',
    status: 'Active',
    primaryPhotoUrl: null,
    availableToday: true,
    ...overrides,
  };
}

describe('CarsStore', () => {
  let api: {
    getCars: ReturnType<typeof vi.fn>;
    getCar: ReturnType<typeof vi.fn>;
    getCarAvailability: ReturnType<typeof vi.fn>;
    createCar: ReturnType<typeof vi.fn>;
    updateCar: ReturnType<typeof vi.fn>;
    retireCar: ReturnType<typeof vi.fn>;
  };
  let store: CarsStore;

  beforeEach(() => {
    api = {
      getCars: vi.fn(),
      getCar: vi.fn(),
      getCarAvailability: vi.fn(),
      createCar: vi.fn(),
      updateCar: vi.fn(),
      retireCar: vi.fn(),
    };

    TestBed.configureTestingModule({ providers: [{ provide: ApiService, useValue: api }] });
    store = TestBed.inject(CarsStore);
  });

  it('loadCars populates the list and clears loading', async () => {
    const cars = [car({ id: 'c1' }), car({ id: 'c2', category: 'Suv' })];
    api.getCars.mockResolvedValue(cars);

    await store.loadCars();

    expect(store.cars()).toEqual(cars);
    expect(store.loading()).toBe(false);
    expect(store.error()).toBeNull();
  });

  it('a failed load surfaces the message and leaves the list untouched', async () => {
    api.getCars.mockRejectedValue(new Error('Cannot reach the server.'));

    await store.loadCars();

    expect(store.error()).toBe('Cannot reach the server.');
    expect(store.cars()).toEqual([]);
    expect(store.loading()).toBe(false);
  });

  describe('visibleCars', () => {
    beforeEach(async () => {
      api.getCars.mockResolvedValue([
        car({ id: 'c1', category: 'Sedan' }),
        car({ id: 'c2', category: 'Suv' }),
        car({ id: 'c3', category: 'Sedan' }),
      ]);
      await store.loadCars();
    });

    it('shows everything when no category filter is set', () => {
      expect(store.visibleCars()).toHaveLength(3);
    });

    it('filters to the selected category without refetching', () => {
      store.setCategoryFilter('Sedan');

      expect(store.visibleCars().map((c) => c.id)).toEqual(['c1', 'c3']);
      expect(api.getCars).toHaveBeenCalledTimes(1);
    });

    it('clearing the filter (null) restores the full list', () => {
      store.setCategoryFilter('Sedan');
      store.setCategoryFilter(null);

      expect(store.visibleCars()).toHaveLength(3);
    });
  });

  it('availableCount counts only cars free today, regardless of the category filter', async () => {
    api.getCars.mockResolvedValue([
      car({ id: 'c1', availableToday: true }),
      car({ id: 'c2', availableToday: false }),
      car({ id: 'c3', availableToday: true }),
    ]);
    await store.loadCars();

    expect(store.availableCount()).toBe(2);
  });

  describe('isEmpty', () => {
    it('is false while a load is in flight, even with zero cars so far', () => {
      api.getCars.mockReturnValue(new Promise(() => {}));
      void store.loadCars();

      expect(store.isEmpty()).toBe(false);
    });

    it('is true once loading has finished with no results', async () => {
      api.getCars.mockResolvedValue([]);
      await store.loadCars();

      expect(store.isEmpty()).toBe(true);
    });

    it('is true when a category filter matches nothing, even though the fleet is not empty', async () => {
      api.getCars.mockResolvedValue([car({ category: 'Sedan' })]);
      await store.loadCars();

      store.setCategoryFilter('Bus');

      expect(store.isEmpty()).toBe(true);
    });
  });

  describe('availability sets', () => {
    it('bookedDateSet and pendingDateSet are empty before any availability has loaded', () => {
      expect(store.bookedDateSet().size).toBe(0);
      expect(store.pendingDateSet().size).toBe(0);
    });

    it('reflects the loaded availability as Sets, for O(1) calendar-cell lookups', async () => {
      const availability: CarAvailability = {
        carId: 'c1',
        carName: 'Test Car',
        windowStart: '2026-10-01',
        windowEnd: '2026-10-31',
        bookedDates: ['2026-10-05', '2026-10-06'],
        pendingDates: ['2026-10-10'],
        carIsBookable: true,
      };
      api.getCarAvailability.mockResolvedValue(availability);

      await store.loadAvailability('c1');

      expect(store.bookedDateSet().has('2026-10-05')).toBe(true);
      expect(store.bookedDateSet().has('2026-10-10')).toBe(false);
      expect(store.pendingDateSet().has('2026-10-10')).toBe(true);
    });
  });

  it('clearSelection drops both the selected car and its availability together', async () => {
    api.getCar.mockResolvedValue({ id: 'c1' });
    api.getCarAvailability.mockResolvedValue({ bookedDates: [], pendingDates: [] });
    await store.loadCar('c1');
    await store.loadAvailability('c1');

    store.clearSelection();

    expect(store.selected()).toBeNull();
    expect(store.availability()).toBeNull();
  });

  describe('admin mutations', () => {
    const createRequest: CreateCarRequest = {
      name: 'New Car',
      description: '',
      category: 'Sedan',
      seats: 4,
      rate: 150,
      pricingModel: 'PerDay',
    };

    it('createCar reloads the list and returns the created car', async () => {
      const created: CarDetail = {
        id: 'new-id',
        name: 'New Car',
        description: '',
        category: 'Sedan',
        seats: 4,
        rate: 150,
        pricingModel: 'PerDay',
        status: 'Active',
        licensePlate: null,
        photos: [],
      };
      api.createCar.mockResolvedValue(created);
      api.getCars.mockResolvedValue([car({ id: 'new-id' })]);

      const result = await store.createCar(createRequest);

      expect(result).toEqual(created);
      expect(store.cars().map((c) => c.id)).toEqual(['new-id']);
      expect(store.loading()).toBe(false);
    });

    it('createCar surfaces the error and rethrows, leaving the list untouched', async () => {
      api.createCar.mockRejectedValue(new Error('Name is required.'));
      api.getCars.mockResolvedValue([]);
      await store.loadCars();

      await expect(store.createCar(createRequest)).rejects.toThrow('Name is required.');

      expect(store.error()).toBe('Name is required.');
      expect(api.getCars).toHaveBeenCalledTimes(1); // not reloaded after a failed create
    });

    it('updateCar reloads the list after a successful update', async () => {
      api.updateCar.mockResolvedValue({ id: 'c1' });
      api.getCars.mockResolvedValue([car({ id: 'c1', rate: 200 })]);

      await store.updateCar('c1', { ...createRequest, name: 'Renamed' });

      expect(api.updateCar).toHaveBeenCalledWith('c1', { ...createRequest, name: 'Renamed' });
      expect(store.cars()[0].rate).toBe(200);
    });

    it('retireCar reloads the list after retiring', async () => {
      api.retireCar.mockResolvedValue(undefined);
      api.getCars.mockResolvedValue([car({ id: 'c1', status: 'Retired' })]);

      await store.retireCar('c1');

      expect(api.retireCar).toHaveBeenCalledWith('c1');
      expect(store.cars()[0].status).toBe('Retired');
    });
  });
});
