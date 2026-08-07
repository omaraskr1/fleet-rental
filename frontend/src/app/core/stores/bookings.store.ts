import { Injectable, computed, inject, signal } from '@angular/core';

import { ApiService } from '../services/api.service';
import type { Booking, BookingStatus, CreateBookingRequest, FleetAvailability } from '../models';

/**
 * Booking state for both sides of the app: the client's own requests (feature 3)
 * and the admin queue plus fleet calendar (feature 4).
 */
@Injectable({ providedIn: 'root' })
export class BookingsStore {
  private readonly api = inject(ApiService);

  private readonly _myBookings = signal<Booking[]>([]);
  private readonly _allBookings = signal<Booking[]>([]);
  private readonly _fleet = signal<FleetAvailability | null>(null);
  private readonly _statusFilter = signal<BookingStatus | null>(null);
  private readonly _loading = signal(false);
  private readonly _submitting = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly myBookings = this._myBookings.asReadonly();
  readonly allBookings = this._allBookings.asReadonly();
  readonly fleet = this._fleet.asReadonly();
  readonly statusFilter = this._statusFilter.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly submitting = this._submitting.asReadonly();
  readonly error = this._error.asReadonly();

  /** Badge count on the admin tab. */
  readonly pendingCount = computed(
    () => this._allBookings().filter((b) => b.status === 'Pending').length,
  );

  readonly filteredBookings = computed(() => {
    const filter = this._statusFilter();
    const bookings = this._allBookings();
    return filter ? bookings.filter((b) => b.status === filter) : bookings;
  });

  /** Upcoming approved trips, soonest first — the "my bookings" hero list. */
  readonly upcomingTrips = computed(() => {
    const today = new Date().toISOString().slice(0, 10);
    return this._myBookings()
      .filter((b) => b.status === 'Approved' && b.endDate >= today)
      .sort((a, b) => a.startDate.localeCompare(b.startDate));
  });

  readonly awaitingDecision = computed(() =>
    this._myBookings().filter((b) => b.status === 'Pending'),
  );

  async loadMyBookings(): Promise<void> {
    this._loading.set(true);
    this._error.set(null);

    try {
      this._myBookings.set(await this.api.getMyBookings());
    } catch (error) {
      this._error.set((error as Error).message);
    } finally {
      this._loading.set(false);
    }
  }

  async loadAllBookings(): Promise<void> {
    this._loading.set(true);
    this._error.set(null);

    try {
      this._allBookings.set(await this.api.getAllBookings());
    } catch (error) {
      this._error.set((error as Error).message);
    } finally {
      this._loading.set(false);
    }
  }

  async loadFleetAvailability(from?: string, to?: string): Promise<void> {
    this._loading.set(true);

    try {
      this._fleet.set(await this.api.getFleetAvailability(from, to));
    } catch (error) {
      this._error.set((error as Error).message);
    } finally {
      this._loading.set(false);
    }
  }

  /** Submits a request. Throws so the form can stay open on failure. */
  async submit(request: CreateBookingRequest): Promise<Booking> {
    this._submitting.set(true);
    this._error.set(null);

    try {
      const booking = await this.api.createBooking(request);
      this._myBookings.update((list) => [booking, ...list]);
      return booking;
    } catch (error) {
      this._error.set((error as Error).message);
      throw error;
    } finally {
      this._submitting.set(false);
    }
  }

  async approve(id: string, reason?: string): Promise<void> {
    await this.decide(() => this.api.approveBooking(id, reason));
  }

  async reject(id: string, reason?: string): Promise<void> {
    await this.decide(() => this.api.rejectBooking(id, reason));
  }

  async cancel(id: string): Promise<void> {
    const updated = await this.api.cancelBooking(id);
    this.replace(updated);
  }

  setStatusFilter(status: BookingStatus | null): void {
    this._statusFilter.set(status);
  }

  clearError(): void {
    this._error.set(null);
  }

  private async decide(operation: () => Promise<Booking>): Promise<void> {
    this._submitting.set(true);
    this._error.set(null);

    try {
      this.replace(await operation());
    } catch (error) {
      this._error.set((error as Error).message);

      // A 409 means someone else claimed the dates, so the cached queue is now
      // wrong. Refetch rather than leave the admin looking at stale rows.
      await this.loadAllBookings();
      throw error;
    } finally {
      this._submitting.set(false);
    }
  }

  private replace(booking: Booking): void {
    const swap = (list: Booking[]) => list.map((b) => (b.id === booking.id ? booking : b));
    this._allBookings.update(swap);
    this._myBookings.update(swap);
  }
}
