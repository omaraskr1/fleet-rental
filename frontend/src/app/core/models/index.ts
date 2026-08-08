/**
 * Mirrors the API's DTOs. Enum-like fields arrive as strings because the API
 * serialises enums with JsonStringEnumConverter — so these are string unions
 * rather than TypeScript enums, and a value the backend adds shows up as a
 * compile error here instead of a silent mismatch.
 */

export type CarCategory =
  | 'Sedan'
  | 'Suv'
  | 'Van'
  | 'Luxury'
  | 'Convertible'
  | 'BrandedTruck'
  | 'Bus';

export type CarStatus = 'Active' | 'Maintenance' | 'Retired';

export type BookingStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';

export type UserRole = 'Client' | 'Admin';

export type EventType =
  | 'ProductLaunch'
  | 'TradeShow'
  | 'Wedding'
  | 'CorporateEvent'
  | 'Photoshoot'
  | 'RoadShow'
  | 'Conference'
  | 'Other';

export type DevicePlatform = 'Ios' | 'Android' | 'Web';

/** ISO date with no time component, e.g. "2026-10-01". */
export type IsoDate = string;

export interface CarListItem {
  id: string;
  name: string;
  category: CarCategory;
  seats: number;
  dailyRate: number;
  status: CarStatus;
  primaryPhotoUrl: string | null;
  availableToday: boolean;
}

export interface CarPhoto {
  id: string;
  url: string;
  caption: string | null;
  isPrimary: boolean;
}

export interface CarDetail {
  id: string;
  name: string;
  description: string;
  category: CarCategory;
  seats: number;
  dailyRate: number;
  status: CarStatus;
  licensePlate: string | null;
  photos: CarPhoto[];
}

export interface CarAvailability {
  carId: string;
  carName: string;
  windowStart: IsoDate;
  windowEnd: IsoDate;
  /** Days held by an approved booking. Everything else in the window is open. */
  bookedDates: IsoDate[];
  /** Days with an undecided request — shown as "contested", not blocked. */
  pendingDates: IsoDate[];
  carIsBookable: boolean;
}

export interface FleetAvailability {
  windowStart: IsoDate;
  windowEnd: IsoDate;
  cars: CarAvailability[];
}

export interface AvailabilityCheck {
  isAvailable: boolean;
  conflictingDates: IsoDate[];
  reason: string | null;
}

export interface EventSummary {
  id: string;
  name: string;
  type: EventType;
  location: string;
  expectedAttendance: number | null;
  notes: string | null;
}

export interface Booking {
  id: string;
  carId: string;
  carName: string;
  carPhotoUrl: string | null;
  clientId: string;
  clientName: string;
  clientEmail: string;
  startDate: IsoDate;
  endDate: IsoDate;
  totalDays: number;
  status: BookingStatus;
  clientNotes: string | null;
  event: EventSummary;
  decidedAt: string | null;
  decisionReason: string | null;
  createdAt: string;
}

export interface CreateBookingRequest {
  carId: string;
  startDate: IsoDate;
  endDate: IsoDate;
  clientNotes?: string | null;
  /** Set to attach another car to an activation the client already registered. */
  existingEventId?: string | null;
  eventName?: string | null;
  eventType?: EventType | null;
  eventLocation?: string | null;
  expectedAttendance?: number | null;
  eventNotes?: string | null;
}

export interface AppUser {
  id: string;
  email: string;
  fullName: string;
  phoneNumber: string | null;
  role: UserRole;
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: AppUser;
}

export interface CreateCarRequest {
  name: string;
  description: string;
  category: CarCategory;
  seats: number;
  dailyRate: number;
  licensePlate?: string | null;
  photoUrls?: string[];
}

export interface UpdateCarRequest extends CreateCarRequest {
  status?: CarStatus | null;
}

/** RFC 7807 problem response, as emitted by the API's exception middleware. */
export interface ProblemDetails {
  status: number;
  title: string;
  detail: string;
  instance?: string;
  errors?: Record<string, string[]>;
}

export const CAR_CATEGORIES: CarCategory[] = [
  'Sedan',
  'Suv',
  'Van',
  'Luxury',
  'Convertible',
  'BrandedTruck',
  'Bus',
];

export const EVENT_TYPES: EventType[] = [
  'ProductLaunch',
  'TradeShow',
  'Wedding',
  'CorporateEvent',
  'Photoshoot',
  'RoadShow',
  'Conference',
  'Other',
];

// ---------- Maintenance ----------

export type IssueSeverity = 'Low' | 'Medium' | 'High' | 'Critical';

export type IssueStatus = 'Open' | 'InProgress' | 'Resolved';

export const ISSUE_SEVERITIES: IssueSeverity[] = ['Low', 'Medium', 'High', 'Critical'];

export interface ServiceRecord {
  id: string;
  carId: string;
  performedAt: IsoDate;
  description: string;
  odometerKm: number | null;
  cost: number;
  performedBy: string | null;
}

export interface LogServiceRequest {
  performedAt: IsoDate;
  description: string;
  odometerKm: number | null;
  cost: number;
  performedBy?: string | null;
}

export interface VehicleIssue {
  id: string;
  carId: string;
  carName: string;
  reportedByName: string;
  description: string;
  severity: IssueSeverity;
  status: IssueStatus;
  reportedAt: string;
  resolvedAt: string | null;
  resolutionNotes: string | null;
}

export interface ReportIssueRequest {
  description: string;
  severity: IssueSeverity;
}

export interface CarMaintenanceSummary {
  carId: string;
  carName: string;
  currentOdometerKm: number | null;
  serviceIntervalKm: number | null;
  lastServiceAt: IsoDate | null;
  kmSinceLastService: number | null;
  isServiceDue: boolean;
  openIssueCount: number;
  hasBlockingIssue: boolean;
}

// ---------- Analytics ----------

/**
 * Every revenue figure here is estimated as Car.DailyRate x booked days —
 * Phase 1 takes no payment, so there is no billed amount to report instead.
 */
export interface AnalyticsOverview {
  from: IsoDate;
  to: IsoDate;
  totalCars: number;
  activeCars: number;
  totalBookings: number;
  pendingBookings: number;
  approvedBookings: number;
  rejectedBookings: number;
  cancelledBookings: number;
  approvalRatePercent: number;
  estimatedRevenue: number;
  fleetUtilizationPercent: number;
  openIssueCount: number;
  criticalIssueCount: number;
  carsServiceDue: number;
  maintenanceCost: number;
}

export interface RevenuePoint {
  periodLabel: string;
  periodStart: IsoDate;
  estimatedRevenue: number;
  approvedBookings: number;
}

export interface CarUtilization {
  carId: string;
  carName: string;
  bookedDays: number;
  daysInRange: number;
  utilizationPercent: number;
  bookingCount: number;
  estimatedRevenue: number;
}

export interface EventTypeBreakdown {
  eventType: EventType;
  bookingCount: number;
  approvedCount: number;
  estimatedRevenue: number;
}

export interface BookingApprovalPrediction {
  bookingId: string;
  /** Calibrated 0..1 — roughly the share of similar past requests that were approved. */
  approvalProbability: number;
}

/**
 * hasSufficientData is false until the fleet has decided enough bookings, both
 * ways, for there to be a pattern worth imitating — predictions is empty in that
 * case rather than filled with noise. trainedOnBookings and minimumRequired let
 * the UI explain why there is nothing to show yet.
 */
export interface BookingApprovalPredictions {
  hasSufficientData: boolean;
  trainedOnBookings: number;
  minimumRequired: number;
  predictions: BookingApprovalPrediction[];
}

export interface MaintenanceCostPoint {
  periodLabel: string;
  periodStart: IsoDate;
  totalCost: number;
  recordCount: number;
}

export type CarProfitabilityRecommendation = 'Keep' | 'Review' | 'ConsiderRetiring';

/**
 * Revenue here is still the DailyRate x booked days estimate, so "profit" is
 * directional — useful for ranking cars against each other, not for the books.
 * Only maintenanceCost is real money.
 */
/** 'Unknown' means not enough settled months to say — deliberately not 'Steady'. */
export type DemandTrend = 'Unknown' | 'Rising' | 'Steady' | 'Declining';

export interface DemandPoint {
  periodLabel: string;
  periodStart: IsoDate;
  bookedDays: number;
}

/**
 * Forecast demand for one vehicle category paired with current capacity — the two
 * together answer "buy another of these, or is one already sitting idle". Demand is
 * booked car-days rather than revenue, so a price rise can't masquerade as growth.
 */
export interface CategoryDemand {
  category: CarCategory;
  carCount: number;
  hasSufficientHistory: boolean;
  history: DemandPoint[];
  forecast: DemandPoint[];
  trend: DemandTrend;
  recentMonthlyAverage: number;
  forecastMonthlyAverage: number;
}

export interface CarProfitability {
  carId: string;
  carName: string;
  estimatedRevenue: number;
  maintenanceCost: number;
  netProfit: number;
  /** Null when there was no revenue to take a percentage of — not the same as 0%. */
  profitMarginPercent: number | null;
  utilizationPercent: number;
  bookingCount: number;
  recommendation: CarProfitabilityRecommendation;
}

export interface RevenueForecastPoint {
  periodLabel: string;
  periodStart: IsoDate;
  forecastedRevenue: number;
  lowerBound: number;
  upperBound: number;
}

/**
 * HasSufficientHistory is false whenever there are fewer than six settled months
 * on file — Forecast is empty in that case rather than a guess dressed up as a
 * number, so the UI must check it before rendering anything.
 */
export interface RevenueForecast {
  hasSufficientHistory: boolean;
  history: RevenuePoint[];
  forecast: RevenueForecastPoint[];
}
