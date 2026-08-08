import { Injectable, computed, inject, signal } from '@angular/core';

import { ApiService } from '../services/api.service';
import type {
  CarAvailability,
  CarCategory,
  CarDetail,
  CarListItem,
  CreateCarRequest,
  UpdateCarRequest,
} from '../models';

/**
 * Fleet browse state (features 1 and 2).
 */
@Injectable({ providedIn: 'root' })
export class CarsStore {
  private readonly api = inject(ApiService);

  private readonly _cars = signal<CarListItem[]>([]);
  private readonly _selected = signal<CarDetail | null>(null);
  private readonly _availability = signal<CarAvailability | null>(null);
  private readonly _categoryFilter = signal<CarCategory | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly cars = this._cars.asReadonly();
  readonly selected = this._selected.asReadonly();
  readonly availability = this._availability.asReadonly();
  readonly categoryFilter = this._categoryFilter.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  /**
   * Filtering happens here rather than by refetching, so tapping a category chip
   * is instant instead of a network round-trip.
   */
  readonly visibleCars = computed(() => {
    const filter = this._categoryFilter();
    const cars = this._cars();
    return filter ? cars.filter((c) => c.category === filter) : cars;
  });

  readonly availableCount = computed(() => this._cars().filter((c) => c.availableToday).length);

  readonly isEmpty = computed(() => !this._loading() && this.visibleCars().length === 0);

  /** Booked days as a Set, so calendar cells test membership in O(1). */
  readonly bookedDateSet = computed(() => new Set(this._availability()?.bookedDates ?? []));

  readonly pendingDateSet = computed(() => new Set(this._availability()?.pendingDates ?? []));

  async loadCars(): Promise<void> {
    this._loading.set(true);
    this._error.set(null);

    try {
      this._cars.set(await this.api.getCars());
    } catch (error) {
      this._error.set((error as Error).message);
    } finally {
      this._loading.set(false);
    }
  }

  async loadCar(id: string): Promise<void> {
    this._loading.set(true);
    this._error.set(null);

    try {
      this._selected.set(await this.api.getCar(id));
    } catch (error) {
      this._error.set((error as Error).message);
    } finally {
      this._loading.set(false);
    }
  }

  async loadAvailability(carId: string, from?: string, to?: string): Promise<void> {
    try {
      this._availability.set(await this.api.getCarAvailability(carId, from, to));
    } catch (error) {
      this._error.set((error as Error).message);
    }
  }

  setCategoryFilter(category: CarCategory | null): void {
    this._categoryFilter.set(category);
  }

  clearSelection(): void {
    this._selected.set(null);
    this._availability.set(null);
  }

  /** Admin fleet management (feature: car add/edit/retire). */

  async createCar(request: CreateCarRequest): Promise<CarDetail> {
    this._loading.set(true);
    this._error.set(null);

    try {
      const created = await this.api.createCar(request);
      await this.loadCars();
      return created;
    } catch (error) {
      this._error.set((error as Error).message);
      throw error;
    } finally {
      this._loading.set(false);
    }
  }

  async updateCar(id: string, request: UpdateCarRequest): Promise<CarDetail> {
    this._loading.set(true);
    this._error.set(null);

    try {
      const updated = await this.api.updateCar(id, request);
      await this.loadCars();
      return updated;
    } catch (error) {
      this._error.set((error as Error).message);
      throw error;
    } finally {
      this._loading.set(false);
    }
  }

  async retireCar(id: string): Promise<void> {
    this._loading.set(true);
    this._error.set(null);

    try {
      await this.api.retireCar(id);
      await this.loadCars();
    } catch (error) {
      this._error.set((error as Error).message);
      throw error;
    } finally {
      this._loading.set(false);
    }
  }
}
