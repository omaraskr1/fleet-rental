import { Injectable, inject, signal } from '@angular/core';

import { ApiService } from '../services/api.service';
import type { CreatePlatformCarRequest, PlatformCar, UpdateCarRequest } from '../models';

/** Cross-company fleet monitoring and management for the platform panel. */
@Injectable({ providedIn: 'root' })
export class PlatformCarsStore {
  private readonly api = inject(ApiService);

  private readonly _cars = signal<PlatformCar[]>([]);
  private readonly _loading = signal(false);
  private readonly _submitting = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly cars = this._cars.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly submitting = this._submitting.asReadonly();
  readonly error = this._error.asReadonly();

  async loadCars(): Promise<void> {
    this._loading.set(true);
    this._error.set(null);

    try {
      this._cars.set(await this.api.getPlatformCars());
    } catch (error) {
      this._error.set((error as Error).message);
    } finally {
      this._loading.set(false);
    }
  }

  async createCar(request: CreatePlatformCarRequest): Promise<PlatformCar> {
    this._submitting.set(true);
    this._error.set(null);

    try {
      const created = await this.api.createPlatformCar(request);
      await this.loadCars();
      return created;
    } catch (error) {
      this._error.set((error as Error).message);
      throw error;
    } finally {
      this._submitting.set(false);
    }
  }

  async updateCar(id: string, request: UpdateCarRequest): Promise<PlatformCar> {
    this._submitting.set(true);
    this._error.set(null);

    try {
      const updated = await this.api.updatePlatformCar(id, request);
      await this.loadCars();
      return updated;
    } catch (error) {
      this._error.set((error as Error).message);
      throw error;
    } finally {
      this._submitting.set(false);
    }
  }

  async retireCar(id: string): Promise<void> {
    this._error.set(null);

    try {
      await this.api.retirePlatformCar(id);
      await this.loadCars();
    } catch (error) {
      this._error.set((error as Error).message);
      throw error;
    }
  }
}
