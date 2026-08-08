import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import type {
  AnalyticsOverview,
  AuthResponse,
  AvailabilityCheck,
  Booking,
  BookingStatus,
  CarAvailability,
  CarDetail,
  CarListItem,
  CarMaintenanceSummary,
  CarProfitability,
  CarUtilization,
  CreateBookingRequest,
  CreateCarRequest,
  DevicePlatform,
  EventTypeBreakdown,
  FleetAvailability,
  IsoDate,
  IssueStatus,
  LogServiceRequest,
  MaintenanceCostPoint,
  ReportIssueRequest,
  RevenueForecast,
  RevenuePoint,
  ServiceRecord,
  UpdateCarRequest,
  VehicleIssue,
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

  // ---------- Maintenance ----------

  getMaintenanceSummary(carId: string): Promise<CarMaintenanceSummary> {
    return firstValueFrom(
      this.http.get<CarMaintenanceSummary>(`${this.base}/cars/${carId}/maintenance`),
    );
  }

  getServiceHistory(carId: string): Promise<ServiceRecord[]> {
    return firstValueFrom(
      this.http.get<ServiceRecord[]>(`${this.base}/cars/${carId}/service-records`),
    );
  }

  logService(carId: string, request: LogServiceRequest): Promise<ServiceRecord> {
    return firstValueFrom(
      this.http.post<ServiceRecord>(`${this.base}/cars/${carId}/service-records`, request),
    );
  }

  updateOdometer(carId: string, km: number): Promise<void> {
    return firstValueFrom(this.http.put<void>(`${this.base}/cars/${carId}/odometer`, { km }));
  }

  setServiceInterval(carId: string, km: number | null): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(`${this.base}/cars/${carId}/service-interval`, { km }),
    );
  }

  reportIssue(carId: string, request: ReportIssueRequest): Promise<VehicleIssue> {
    return firstValueFrom(
      this.http.post<VehicleIssue>(`${this.base}/cars/${carId}/issues`, request),
    );
  }

  getIssues(carId?: string, status?: IssueStatus): Promise<VehicleIssue[]> {
    let params = new HttpParams();
    if (carId) params = params.set('carId', carId);
    if (status) params = params.set('status', status);
    return firstValueFrom(this.http.get<VehicleIssue[]>(`${this.base}/issues`, { params }));
  }

  startIssueProgress(issueId: string): Promise<VehicleIssue> {
    return firstValueFrom(
      this.http.post<VehicleIssue>(`${this.base}/issues/${issueId}/start-progress`, {}),
    );
  }

  resolveIssue(issueId: string, resolutionNotes?: string): Promise<VehicleIssue> {
    return firstValueFrom(
      this.http.post<VehicleIssue>(`${this.base}/issues/${issueId}/resolve`, { resolutionNotes }),
    );
  }

  reopenIssue(issueId: string): Promise<VehicleIssue> {
    return firstValueFrom(
      this.http.post<VehicleIssue>(`${this.base}/issues/${issueId}/reopen`, {}),
    );
  }

  // ---------- Analytics ----------

  getAnalyticsOverview(from?: IsoDate, to?: IsoDate): Promise<AnalyticsOverview> {
    return firstValueFrom(
      this.http.get<AnalyticsOverview>(`${this.base}/analytics/overview`, {
        params: this.rangeParams(from, to),
      }),
    );
  }

  getRevenueTrend(from?: IsoDate, to?: IsoDate): Promise<RevenuePoint[]> {
    return firstValueFrom(
      this.http.get<RevenuePoint[]>(`${this.base}/analytics/revenue`, {
        params: this.rangeParams(from, to),
      }),
    );
  }

  getUtilization(from?: IsoDate, to?: IsoDate): Promise<CarUtilization[]> {
    return firstValueFrom(
      this.http.get<CarUtilization[]>(`${this.base}/analytics/utilization`, {
        params: this.rangeParams(from, to),
      }),
    );
  }

  getEventTypeBreakdown(from?: IsoDate, to?: IsoDate): Promise<EventTypeBreakdown[]> {
    return firstValueFrom(
      this.http.get<EventTypeBreakdown[]>(`${this.base}/analytics/event-types`, {
        params: this.rangeParams(from, to),
      }),
    );
  }

  getMaintenanceCostTrend(from?: IsoDate, to?: IsoDate): Promise<MaintenanceCostPoint[]> {
    return firstValueFrom(
      this.http.get<MaintenanceCostPoint[]>(`${this.base}/analytics/maintenance-costs`, {
        params: this.rangeParams(from, to),
      }),
    );
  }

  getProfitability(from?: IsoDate, to?: IsoDate): Promise<CarProfitability[]> {
    return firstValueFrom(
      this.http.get<CarProfitability[]>(`${this.base}/analytics/profitability`, {
        params: this.rangeParams(from, to),
      }),
    );
  }

  getRevenueForecast(months = 3): Promise<RevenueForecast> {
    return firstValueFrom(
      this.http.get<RevenueForecast>(`${this.base}/analytics/revenue-forecast`, {
        params: new HttpParams().set('months', months),
      }),
    );
  }

  private rangeParams(from?: IsoDate, to?: IsoDate): HttpParams {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return params;
  }
}
