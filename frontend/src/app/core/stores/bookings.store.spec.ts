import { TestBed } from '@angular/core/testing';

import { BookingsStore } from './bookings.store';
import { ApiService } from '../services/api.service';
import type { Booking, BookingStatus } from '../models';

function booking(overrides: Partial<Booking> = {}): Booking {
  return {
    id: 'b1',
    carId: 'c1',
    carName: 'Test Car',
    carPhotoUrl: null,
    clientId: 'u1',
    clientName: 'Test Client',
    clientEmail: 'client@test.com',
    startDate: '2026-10-01',
    endDate: '2026-10-05',
    totalDays: 5,
    status: 'Pending',
    clientNotes: null,
    event: {
      id: 'e1',
      name: 'Test Event',
      type: 'Other',
      location: 'Test Location',
      expectedAttendance: null,
      notes: null,
    },
    decidedAt: null,
    decisionReason: null,
    createdAt: '2026-08-01T00:00:00Z',
    ...overrides,
  };
}

describe('BookingsStore', () => {
  let api: {
    getMyBookings: ReturnType<typeof vi.fn>;
    getAllBookings: ReturnType<typeof vi.fn>;
    getFleetAvailability: ReturnType<typeof vi.fn>;
    createBooking: ReturnType<typeof vi.fn>;
    approveBooking: ReturnType<typeof vi.fn>;
    rejectBooking: ReturnType<typeof vi.fn>;
    cancelBooking: ReturnType<typeof vi.fn>;
  };
  let store: BookingsStore;

  beforeEach(() => {
    api = {
      getMyBookings: vi.fn(),
      getAllBookings: vi.fn(),
      getFleetAvailability: vi.fn(),
      createBooking: vi.fn(),
      approveBooking: vi.fn(),
      rejectBooking: vi.fn(),
      cancelBooking: vi.fn(),
    };

    TestBed.configureTestingModule({ providers: [{ provide: ApiService, useValue: api }] });
    store = TestBed.inject(BookingsStore);
  });

  describe('pendingCount', () => {
    it('counts only Pending bookings across the admin queue', async () => {
      api.getAllBookings.mockResolvedValue([
        booking({ id: 'b1', status: 'Pending' }),
        booking({ id: 'b2', status: 'Approved' }),
        booking({ id: 'b3', status: 'Pending' }),
      ]);

      await store.loadAllBookings();

      expect(store.pendingCount()).toBe(2);
    });
  });

  describe('filteredBookings', () => {
    beforeEach(async () => {
      api.getAllBookings.mockResolvedValue([
        booking({ id: 'b1', status: 'Pending' }),
        booking({ id: 'b2', status: 'Approved' }),
        booking({ id: 'b3', status: 'Rejected' }),
      ]);
      await store.loadAllBookings();
    });

    it('returns everything with no filter set', () => {
      expect(store.filteredBookings()).toHaveLength(3);
    });

    it('narrows to the selected status', () => {
      store.setStatusFilter('Approved');
      expect(store.filteredBookings().map((b) => b.id)).toEqual(['b2']);
    });
  });

  describe('upcomingTrips', () => {
    it('includes only Approved bookings ending today or later, soonest first', async () => {
      const today = new Date().toISOString().slice(0, 10);
      const past = '2000-01-01';
      const future1 = '2099-01-10';
      const future2 = '2099-01-01';

      api.getMyBookings.mockResolvedValue([
        booking({ id: 'gone', status: 'Approved', endDate: past }),
        booking({ id: 'pending', status: 'Pending', endDate: future1 }),
        booking({ id: 'later', status: 'Approved', startDate: future1, endDate: future1 }),
        booking({ id: 'sooner', status: 'Approved', startDate: future2, endDate: future2 }),
        booking({ id: 'today', status: 'Approved', startDate: today, endDate: today }),
      ]);

      await store.loadMyBookings();

      expect(store.upcomingTrips().map((b) => b.id)).toEqual(['today', 'sooner', 'later']);
    });
  });

  describe('awaitingDecision', () => {
    it('is exactly the client\'s Pending bookings', async () => {
      api.getMyBookings.mockResolvedValue([
        booking({ id: 'b1', status: 'Pending' }),
        booking({ id: 'b2', status: 'Approved' }),
      ]);

      await store.loadMyBookings();

      expect(store.awaitingDecision().map((b) => b.id)).toEqual(['b1']);
    });
  });

  describe('submit', () => {
    it('prepends the new booking to myBookings and returns it', async () => {
      const created = booking({ id: 'new-1' });
      api.createBooking.mockResolvedValue(created);
      api.getMyBookings.mockResolvedValue([booking({ id: 'existing' })]);
      await store.loadMyBookings();

      const result = await store.submit({
        carId: 'c1',
        startDate: '2026-10-01',
        endDate: '2026-10-05',
      });

      expect(result).toEqual(created);
      expect(store.myBookings().map((b) => b.id)).toEqual(['new-1', 'existing']);
    });

    it('rethrows on failure so the form knows to stay open, and records the message', async () => {
      api.createBooking.mockRejectedValue(new Error('Already booked for part of this range.'));

      await expect(
        store.submit({ carId: 'c1', startDate: '2026-10-01', endDate: '2026-10-05' }),
      ).rejects.toThrow('Already booked');

      expect(store.error()).toBe('Already booked for part of this range.');
      expect(store.submitting()).toBe(false);
    });
  });

  describe('approve / reject', () => {
    it('approve replaces the booking in both lists with the server response', async () => {
      const original = booking({ id: 'b1', status: 'Pending' });
      const approved = { ...original, status: 'Approved' as BookingStatus };

      api.getAllBookings.mockResolvedValue([original]);
      api.getMyBookings.mockResolvedValue([original]);
      await store.loadAllBookings();
      await store.loadMyBookings();

      api.approveBooking.mockResolvedValue(approved);
      await store.approve('b1', 'Confirmed');

      expect(store.allBookings()[0].status).toBe('Approved');
      expect(store.myBookings()[0].status).toBe('Approved');
      expect(api.approveBooking).toHaveBeenCalledWith('b1', 'Confirmed');
    });

    it('a 409 conflict refetches the queue so stale rows are not left on screen, then rethrows', async () => {
      api.getAllBookings.mockResolvedValueOnce([booking({ id: 'b1', status: 'Pending' })]);
      await store.loadAllBookings();

      api.approveBooking.mockRejectedValue(new Error('Another approval claimed these dates.'));
      api.getAllBookings.mockResolvedValueOnce([booking({ id: 'b1', status: 'Rejected' })]);

      await expect(store.approve('b1')).rejects.toThrow('Another approval');

      expect(api.getAllBookings).toHaveBeenCalledTimes(2);
      expect(store.allBookings()[0].status).toBe('Rejected');
      expect(store.error()).toBe('Another approval claimed these dates.');
    });

    it('reject replaces the booking the same way approve does', async () => {
      const original = booking({ id: 'b1', status: 'Pending' });
      api.getAllBookings.mockResolvedValue([original]);
      await store.loadAllBookings();

      api.rejectBooking.mockResolvedValue({ ...original, status: 'Rejected' as BookingStatus });
      await store.reject('b1', 'No longer available');

      expect(store.allBookings()[0].status).toBe('Rejected');
    });
  });

  describe('cancel', () => {
    it('replaces the booking with the cancelled version from the server', async () => {
      const original = booking({ id: 'b1', status: 'Approved' });
      api.getMyBookings.mockResolvedValue([original]);
      await store.loadMyBookings();

      api.cancelBooking.mockResolvedValue({ ...original, status: 'Cancelled' as BookingStatus });
      await store.cancel('b1');

      expect(store.myBookings()[0].status).toBe('Cancelled');
    });
  });

  it('clearError resets only the error signal', async () => {
    api.createBooking.mockRejectedValue(new Error('boom'));
    await expect(
      store.submit({ carId: 'c1', startDate: '2026-10-01', endDate: '2026-10-02' }),
    ).rejects.toThrow();

    store.clearError();

    expect(store.error()).toBeNull();
  });
});
