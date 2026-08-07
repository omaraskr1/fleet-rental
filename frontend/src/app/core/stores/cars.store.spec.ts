import { TestBed } from '@angular/core/testing';

import { CarsStore } from './cars.store';
import { ApiService } from '../services/api.service';
import type { CarAvailability, CarListItem } from '../models';

function car(overrides: Partial<CarListItem> = {}): CarListItem {
  return {
    id: 'c1',
    name: 'Test Car',
    category: 'Sedan',
    seats: 4,
    dailyRate: 100,
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
  };
  let store: CarsStore;

  beforeEach(() => {
    api = { getCars: vi.fn(), getCar: vi.fn(), getCarAvailability: vi.fn() };

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
});
