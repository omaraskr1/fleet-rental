import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import type {
  AuthResponse,
  AvailabilityCheck,
  Booking,
  BookingStatus,
  CarAvailability,
  CarDetail,
  CarListItem,
  CreateBookingRequest,
  CreateCarRequest,
  DevicePlatform,
  FleetAvailability,
  IsoDate,
  UpdateCarRequest,
  AppUser,
} from '../models';

/**
 * Thin typed wrapper over the REST API. Deliberately holds no state — the
 * stores own that. Returns promises rather than observables because every call
 * site here is a one-shot request awaited inside a signal-based store.
 */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  // ---------- Cars ----------

  getCars(category?: string): Promise<CarListItem[]> {
    let params = new HttpParams();
    if (category) {
      params = params.set('category', category);
    }
    return firstValueFrom(this.http.get<CarListItem[]>(`${this.base}/cars`, { params }));
  }

  getCar(id: string): Promise<CarDetail> {
    return firstValueFrom(this.http.get<CarDetail>(`${this.base}/cars/${id}`));
  }

  getCarAvailability(id: string, from?: IsoDate, to?: IsoDate): Promise<CarAvailability> {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return firstValueFrom(
      this.http.get<CarAvailability>(`${this.base}/cars/${id}/availability`, { params }),
    );
  }

  checkAvailability(id: string, from: IsoDate, to: IsoDate): Promise<AvailabilityCheck> {
    const params = new HttpParams().set('from', from).set('to', to);
    return firstValueFrom(
      this.http.get<AvailabilityCheck>(`${this.base}/cars/${id}/availability/check`, { params }),
    );
  }

  createCar(request: CreateCarRequest): Promise<CarDetail> {
    return firstValueFrom(this.http.post<CarDetail>(`${this.base}/cars`, request));
  }

  updateCar(id: string, request: UpdateCarRequest): Promise<CarDetail> {
    return firstValueFrom(this.http.put<CarDetail>(`${this.base}/cars/${id}`, request));
  }

  retireCar(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/cars/${id}`));
  }

  // ---------- Bookings ----------

  createBooking(request: CreateBookingRequest): Promise<Booking> {
    return firstValueFrom(this.http.post<Booking>(`${this.base}/bookings`, request));
  }

  getMyBookings(status?: BookingStatus): Promise<Booking[]> {
    let params = new HttpParams();
    if (status) params = params.set('status', status);
    return firstValueFrom(this.http.get<Booking[]>(`${this.base}/bookings/mine`, { params }));
  }

  getBooking(id: string): Promise<Booking> {
    return firstValueFrom(this.http.get<Booking>(`${this.base}/bookings/${id}`));
  }

  cancelBooking(id: string): Promise<Booking> {
    return firstValueFrom(this.http.post<Booking>(`${this.base}/bookings/${id}/cancel`, {}));
  }

  // ---------- Admin ----------

  getAllBookings(status?: BookingStatus, carId?: string): Promise<Booking[]> {
    let params = new HttpParams();
    if (status) params = params.set('status', status);
    if (carId) params = params.set('carId', carId);
    return firstValueFrom(this.http.get<Booking[]>(`${this.base}/bookings`, { params }));
  }

  approveBooking(id: string, reason?: string): Promise<Booking> {
    return firstValueFrom(
      this.http.post<Booking>(`${this.base}/bookings/${id}/approve`, { reason: reason ?? null }),
    );
  }

  rejectBooking(id: string, reason?: string): Promise<Booking> {
    return firstValueFrom(
      this.http.post<Booking>(`${this.base}/bookings/${id}/reject`, { reason: reason ?? null }),
    );
  }

  getFleetAvailability(from?: IsoDate, to?: IsoDate): Promise<FleetAvailability> {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return firstValueFrom(
      this.http.get<FleetAvailability>(`${this.base}/fleet/availability`, { params }),
    );
  }

  // ---------- Auth ----------

  signUp(email: string, password: string, fullName: string, phoneNumber?: string): Promise<AuthResponse> {
    return firstValueFrom(
      this.http.post<AuthResponse>(`${this.base}/auth/signup`, {
        email,
        password,
        fullName,
        phoneNumber: phoneNumber ?? null,
      }),
    );
  }

  login(email: string, password: string): Promise<AuthResponse> {
    return firstValueFrom(this.http.post<AuthResponse>(`${this.base}/auth/login`, { email, password }));
  }

  adminLogin(email: string, password: string): Promise<AuthResponse> {
    return firstValueFrom(
      this.http.post<AuthResponse>(`${this.base}/auth/admin/login`, { email, password }),
    );
  }

  me(): Promise<AppUser> {
    return firstValueFrom(this.http.get<AppUser>(`${this.base}/auth/me`));
  }

  registerDevice(token: string, platform: DevicePlatform, deviceId: string): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`${this.base}/auth/devices`, { token, platform, deviceId }),
    );
  }

  unregisterDevice(deviceId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/auth/devices/${deviceId}`));
  }
}
