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
  CategoryDemand,
  Company,
  CompanyAdmin,
  CreateBookingRequest,
  CreateCarRequest,
  CreateCompanyAdminRequest,
  CreateCompanyRequest,
  CreatePlatformAdminRequest,
  CreatePlatformCarRequest,
  DevicePlatform,
  EventTypeBreakdown,
  FleetAvailability,
  IsoDate,
  IssueStatus,
  LogServiceRequest,
  BookingApprovalPredictions,
  MaintenanceCostPoint,
  PlatformAdmin,
  PlatformAuthResponse,
  PlatformCar,
  ReportIssueRequest,
  RevenueForecast,
  RevenuePoint,
  ServiceRecord,
  ServiceType,
  ServiceTypeStatus,
  CreateServiceTypeRequest,
  UpdateServiceTypeRequest,
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

  // ---------- Service catalog ----------

  getServiceTypes(includeInactive = false): Promise<ServiceType[]> {
    const params = new HttpParams().set('includeInactive', includeInactive);
    return firstValueFrom(this.http.get<ServiceType[]>(`${this.base}/service-types`, { params }));
  }

  createServiceType(request: CreateServiceTypeRequest): Promise<ServiceType> {
    return firstValueFrom(this.http.post<ServiceType>(`${this.base}/service-types`, request));
  }

  updateServiceType(id: string, request: UpdateServiceTypeRequest): Promise<ServiceType> {
    return firstValueFrom(this.http.put<ServiceType>(`${this.base}/service-types/${id}`, request));
  }

  deactivateServiceType(id: string): Promise<ServiceType> {
    return firstValueFrom(this.http.post<ServiceType>(`${this.base}/service-types/${id}/deactivate`, {}));
  }

  reactivateServiceType(id: string): Promise<ServiceType> {
    return firstValueFrom(this.http.post<ServiceType>(`${this.base}/service-types/${id}/reactivate`, {}));
  }

  getServiceTypeStatuses(carId: string): Promise<ServiceTypeStatus[]> {
    return firstValueFrom(
      this.http.get<ServiceTypeStatus[]>(`${this.base}/cars/${carId}/service-type-status`),
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

  getBookingApprovalPredictions(): Promise<BookingApprovalPredictions> {
    return firstValueFrom(
      this.http.get<BookingApprovalPredictions>(`${this.base}/analytics/booking-predictions`),
    );
  }

  getMaintenanceCostTrend(from?: IsoDate, to?: IsoDate): Promise<MaintenanceCostPoint[]> {
    return firstValueFrom(
      this.http.get<MaintenanceCostPoint[]>(`${this.base}/analytics/maintenance-costs`, {
        params: this.rangeParams(from, to),
      }),
    );
  }

  getCategoryDemand(months = 3): Promise<CategoryDemand[]> {
    return firstValueFrom(
      this.http.get<CategoryDemand[]>(`${this.base}/analytics/category-demand`, {
        params: new HttpParams().set('months', months),
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

  // ---------- Platform admin ----------

  platformLogin(email: string, password: string): Promise<PlatformAuthResponse> {
    return firstValueFrom(
      this.http.post<PlatformAuthResponse>(`${this.base}/platform/auth/login`, { email, password }),
    );
  }

  platformMe(): Promise<PlatformAdmin> {
    return firstValueFrom(this.http.get<PlatformAdmin>(`${this.base}/platform/auth/me`));
  }

  getPlatformAdmins(): Promise<PlatformAdmin[]> {
    return firstValueFrom(this.http.get<PlatformAdmin[]>(`${this.base}/platform/admins`));
  }

  createPlatformAdmin(request: CreatePlatformAdminRequest): Promise<PlatformAdmin> {
    return firstValueFrom(this.http.post<PlatformAdmin>(`${this.base}/platform/admins`, request));
  }

  getCompanies(): Promise<Company[]> {
    return firstValueFrom(this.http.get<Company[]>(`${this.base}/platform/companies`));
  }

  createCompany(request: CreateCompanyRequest): Promise<Company> {
    return firstValueFrom(this.http.post<Company>(`${this.base}/platform/companies`, request));
  }

  suspendCompany(tenantId: string): Promise<Company> {
    return firstValueFrom(
      this.http.post<Company>(`${this.base}/platform/companies/${tenantId}/suspend`, {}),
    );
  }

  reactivateCompany(tenantId: string): Promise<Company> {
    return firstValueFrom(
      this.http.post<Company>(`${this.base}/platform/companies/${tenantId}/reactivate`, {}),
    );
  }

  getCompanyAdmins(tenantId: string): Promise<CompanyAdmin[]> {
    return firstValueFrom(
      this.http.get<CompanyAdmin[]>(`${this.base}/platform/companies/${tenantId}/admins`),
    );
  }

  createCompanyAdmin(tenantId: string, request: CreateCompanyAdminRequest): Promise<CompanyAdmin> {
    return firstValueFrom(
      this.http.post<CompanyAdmin>(`${this.base}/platform/companies/${tenantId}/admins`, request),
    );
  }

  getPlatformCars(): Promise<PlatformCar[]> {
    return firstValueFrom(this.http.get<PlatformCar[]>(`${this.base}/platform/cars`));
  }

  createPlatformCar(request: CreatePlatformCarRequest): Promise<PlatformCar> {
    return firstValueFrom(this.http.post<PlatformCar>(`${this.base}/platform/cars`, request));
  }

  updatePlatformCar(id: string, request: UpdateCarRequest): Promise<PlatformCar> {
    return firstValueFrom(this.http.put<PlatformCar>(`${this.base}/platform/cars/${id}`, request));
  }

  retirePlatformCar(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/platform/cars/${id}`));
  }

  private rangeParams(from?: IsoDate, to?: IsoDate): HttpParams {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return params;
  }
}
