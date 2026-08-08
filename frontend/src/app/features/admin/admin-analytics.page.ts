import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { IonNote, IonSpinner } from '@ionic/angular/standalone';

import { ApiService } from '../../core/services/api.service';
import { LocaleStore } from '../../core/stores/locale.store';
import type {
  AnalyticsOverview,
  CarUtilization,
  EventTypeBreakdown,
  MaintenanceCostPoint,
  RevenueForecast,
  RevenuePoint,
} from '../../core/models';

/**
 * Fleet-wide analytics — the layer the ML work (Phase 3) will sit on top of.
 * Every figure is derived from bookings, cars, and maintenance data other
 * features already collect; revenue is estimated (Car.DailyRate × booked
 * days) since Phase 1 takes no payment. See AnalyticsService on the backend.
 */
@Component({
  selector: 'app-admin-analytics',
  imports: [IonSpinner, IonNote],
  template: `
    <h1>{{ locale.t('admin.analytics.title') }}</h1>

    @if (error(); as message) {
      <ion-note color="danger" class="banner">{{ message }}</ion-note>
    }

    @if (loading()) {
      <div class="state"><ion-spinner /></div>
    } @else if (overview(); as ov) {
      <p class="subtitle">
        {{ locale.formatDate(ov.from, dateOptions) }} – {{ locale.formatDate(ov.to, dateOptions) }}
      </p>

      <div class="tiles">
        <div class="tile">
          <span class="label">{{ locale.t('admin.analytics.estimatedRevenue') }}</span>
          <span class="value">{{ formatMoney(ov.estimatedRevenue) }}</span>
        </div>
        <div class="tile">
          <span class="label">{{ locale.t('admin.analytics.utilization') }}</span>
          <span class="value">{{ ov.fleetUtilizationPercent }}%</span>
        </div>
        <div class="tile">
          <span class="label">{{ locale.t('admin.analytics.approvalRate') }}</span>
          <span class="value">{{ ov.approvalRatePercent }}%</span>
        </div>
        <div class="tile">
          <span class="label">{{ locale.t('admin.analytics.activeCars') }}</span>
          <span class="value">{{ ov.activeCars }} / {{ ov.totalCars }}</span>
        </div>
        <div class="tile">
          <span class="label">{{ locale.t('admin.analytics.bookings') }}</span>
          <span class="value">{{ ov.totalBookings }}</span>
          <span class="sub">
            {{ ov.pendingBookings }} {{ locale.t('admin.analytics.pending') }} ·
            {{ ov.approvedBookings }} {{ locale.t('admin.analytics.approved') }}
          </span>
        </div>
        <div class="tile" [class.warn]="ov.criticalIssueCount > 0">
          <span class="label">{{ locale.t('admin.analytics.openIssues') }}</span>
          <span class="value">{{ ov.openIssueCount }}</span>
          @if (ov.criticalIssueCount > 0) {
            <span class="sub danger">{{ ov.criticalIssueCount }} {{ locale.t('admin.analytics.critical') }}</span>
          }
        </div>
        <div class="tile" [class.warn]="ov.carsServiceDue > 0">
          <span class="label">{{ locale.t('admin.analytics.serviceDue') }}</span>
          <span class="value">{{ ov.carsServiceDue }}</span>
        </div>
        <div class="tile">
          <span class="label">{{ locale.t('admin.analytics.maintenanceCost') }}</span>
          <span class="value">{{ formatMoney(ov.maintenanceCost) }}</span>
        </div>
      </div>

      <section class="chart-card forecast-card">
        <h2>{{ locale.t('admin.analytics.revenueForecast') }}</h2>
        @if (revenueForecast(); as forecast) {
          @if (!forecast.hasSufficientHistory) {
            <p class="state small">{{ locale.t('admin.analytics.forecastInsufficientHistory') }}</p>
          } @else {
            <div class="bars">
              @for (point of forecastChartPoints(); track point.periodStart) {
                <div class="bar-col">
                  <div
                    class="bar"
                    [class.forecast]="point.isForecast"
                    [style.height.%]="barHeight(point.value, maxForecastValue())">
                    <span class="bar-value">{{ formatMoney(point.value) }}</span>
                  </div>
                  <span class="bar-label">{{ point.periodLabel }}</span>
                </div>
              }
            </div>
            <p class="legend">
              <span class="swatch"></span>{{ locale.t('admin.analytics.actual') }}
              <span class="swatch forecast"></span>{{ locale.t('admin.analytics.projected') }}
            </p>
          }
        }
      </section>

      <div class="charts">
        <section class="chart-card">
          <h2>{{ locale.t('admin.analytics.revenueTrend') }}</h2>
          @if (revenueTrend().length === 0) {
            <p class="state small">{{ locale.t('admin.analytics.noData') }}</p>
          } @else {
            <div class="bars">
              @for (point of revenueTrend(); track point.periodStart) {
                <div class="bar-col">
                  <div class="bar" [style.height.%]="barHeight(point.estimatedRevenue, maxRevenue())">
                    <span class="bar-value">{{ formatMoney(point.estimatedRevenue) }}</span>
                  </div>
                  <span class="bar-label">{{ point.periodLabel }}</span>
                </div>
              }
            </div>
          }
        </section>

        <section class="chart-card">
          <h2>{{ locale.t('admin.analytics.maintenanceCostTrend') }}</h2>
          @if (maintenanceTrend().length === 0) {
            <p class="state small">{{ locale.t('admin.analytics.noData') }}</p>
          } @else {
            <div class="bars">
              @for (point of maintenanceTrend(); track point.periodStart) {
                <div class="bar-col">
                  <div class="bar cost" [style.height.%]="barHeight(point.totalCost, maxMaintenanceCost())">
                    <span class="bar-value">{{ formatMoney(point.totalCost) }}</span>
                  </div>
                  <span class="bar-label">{{ point.periodLabel }}</span>
                </div>
              }
            </div>
          }
        </section>

        <section class="chart-card">
          <h2>{{ locale.t('admin.analytics.utilizationByCar') }}</h2>
          @if (utilization().length === 0) {
            <p class="state small">{{ locale.t('admin.analytics.noData') }}</p>
          } @else {
            @for (car of utilization(); track car.carId) {
              <div class="hbar-row">
                <span class="hbar-label link" (click)="openMaintenance(car.carId)">{{ car.carName }}</span>
                <div class="hbar-track">
                  <div class="hbar-fill" [style.width.%]="car.utilizationPercent"></div>
                </div>
                <span class="hbar-value">{{ car.utilizationPercent }}%</span>
              </div>
            }
          }
        </section>

        <section class="chart-card">
          <h2>{{ locale.t('admin.analytics.demandByEventType') }}</h2>
          @if (eventTypes().length === 0) {
            <p class="state small">{{ locale.t('admin.analytics.noData') }}</p>
          } @else {
            @for (row of eventTypes(); track row.eventType) {
              <div class="hbar-row">
                <span class="hbar-label">{{ locale.t('enums.eventType.' + row.eventType) }}</span>
                <div class="hbar-track">
                  <div class="hbar-fill accent" [style.width.%]="barHeight(row.bookingCount, maxEventCount())"></div>
                </div>
                <span class="hbar-value">{{ row.bookingCount }}</span>
              </div>
            }
          }
        </section>
      </div>
    }
  `,
  styles: `
    h1 { font-size: 1.4rem; margin: 0 0 4px; }
    .subtitle { color: var(--ion-color-medium); font-size: 0.85rem; margin: 0 0 20px; }
    .banner { display: block; padding: 12px 0; white-space: pre-line; }
    .state { padding: 48px; text-align: center; color: var(--ion-color-medium); }
    .state.small { padding: 24px; }

    .tiles {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
      gap: 12px;
      margin-bottom: 24px;
    }
    .tile {
      display: flex; flex-direction: column; gap: 2px;
      padding: 14px 16px; border-radius: 10px;
      background: var(--ion-color-light); border: 1px solid var(--ion-color-light-shade);
    }
    .tile.warn { border-color: var(--ion-color-warning); }
    .tile .label { font-size: 0.78rem; color: var(--ion-color-medium); }
    .tile .value { font-size: 1.5rem; font-weight: 700; }
    .tile .sub { font-size: 0.75rem; color: var(--ion-color-medium); }
    .tile .sub.danger { color: var(--ion-color-danger); font-weight: 600; }

    .charts {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
      gap: 16px;
    }
    .chart-card {
      padding: 16px; border-radius: 10px; margin-bottom: 16px;
      background: var(--ion-color-light); border: 1px solid var(--ion-color-light-shade);
    }
    .charts .chart-card { margin-bottom: 0; }
    .chart-card h2 { font-size: 0.95rem; margin: 0 0 14px; }
    .forecast-card { border-color: var(--ion-color-primary); }

    .bars { display: flex; align-items: flex-end; gap: 8px; height: 160px; overflow-x: auto; }
    .bar-col { display: flex; flex-direction: column; align-items: center; justify-content: flex-end;
               height: 100%; min-width: 36px; flex: 1; }
    .bar { width: 100%; min-height: 2px; background: var(--ion-color-primary);
           border-radius: 4px 4px 0 0; position: relative; display: flex; justify-content: center; }
    .bar.cost { background: var(--ion-color-warning); }
    .bar.forecast { background: transparent; border: 2px dashed var(--ion-color-primary); }
    .bar-value { position: absolute; top: -18px; font-size: 0.65rem; white-space: nowrap; color: var(--ion-color-medium); }
    .bar-label { font-size: 0.68rem; color: var(--ion-color-medium); margin-top: 6px; white-space: nowrap; }

    .legend { display: flex; align-items: center; gap: 6px; font-size: 0.72rem;
              color: var(--ion-color-medium); margin: 12px 0 0; }
    .legend .swatch { width: 10px; height: 10px; border-radius: 2px; background: var(--ion-color-primary);
                       display: inline-block; margin-inline-end: 4px; }
    .legend .swatch.forecast { background: transparent; border: 2px dashed var(--ion-color-primary);
                                margin-inline-start: 14px; }

    .hbar-row { display: grid; grid-template-columns: 110px 1fr 50px; align-items: center; gap: 10px; margin-bottom: 10px; }
    .hbar-label { font-size: 0.82rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .hbar-label.link { cursor: pointer; color: var(--ion-color-primary); }
    .hbar-track { height: 10px; border-radius: 5px; background: var(--ion-color-light-shade); overflow: hidden; }
    .hbar-fill { height: 100%; background: var(--ion-color-primary); border-radius: 5px; }
    .hbar-fill.accent { background: var(--ion-color-secondary, var(--ion-color-primary)); }
    .hbar-value { font-size: 0.78rem; text-align: end; color: var(--ion-color-medium); }
  `,
})
export class AdminAnalyticsPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  protected readonly locale = inject(LocaleStore);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly overview = signal<AnalyticsOverview | null>(null);
  protected readonly revenueTrend = signal<RevenuePoint[]>([]);
  protected readonly utilization = signal<CarUtilization[]>([]);
  protected readonly eventTypes = signal<EventTypeBreakdown[]>([]);
  protected readonly maintenanceTrend = signal<MaintenanceCostPoint[]>([]);
  protected readonly revenueForecast = signal<RevenueForecast | null>(null);

  protected readonly maxRevenue = computed(() =>
    Math.max(1, ...this.revenueTrend().map((p) => p.estimatedRevenue)),
  );
  protected readonly maxMaintenanceCost = computed(() =>
    Math.max(1, ...this.maintenanceTrend().map((p) => p.totalCost)),
  );
  protected readonly maxEventCount = computed(() =>
    Math.max(1, ...this.eventTypes().map((r) => r.bookingCount)),
  );

  /** Last 6 settled months, so a fleet with years of history doesn't render an unreadable wall of bars. */
  protected readonly forecastChartPoints = computed(() => {
    const forecast = this.revenueForecast();
    if (!forecast?.hasSufficientHistory) {
      return [];
    }
    const recentHistory = forecast.history.slice(-6).map((p) => ({
      periodStart: p.periodStart,
      periodLabel: p.periodLabel,
      value: p.estimatedRevenue,
      isForecast: false,
    }));
    const projected = forecast.forecast.map((p) => ({
      periodStart: p.periodStart,
      periodLabel: p.periodLabel,
      value: p.forecastedRevenue,
      isForecast: true,
    }));
    return [...recentHistory, ...projected];
  });

  protected readonly maxForecastValue = computed(() =>
    Math.max(1, ...this.forecastChartPoints().map((p) => p.value)),
  );

  async ngOnInit(): Promise<void> {
    try {
      const [overview, revenueTrend, utilization, eventTypes, maintenanceTrend, revenueForecast] =
        await Promise.all([
          this.api.getAnalyticsOverview(),
          this.api.getRevenueTrend(),
          this.api.getUtilization(),
          this.api.getEventTypeBreakdown(),
          this.api.getMaintenanceCostTrend(),
          this.api.getRevenueForecast(),
        ]);
      this.overview.set(overview);
      this.revenueTrend.set(revenueTrend);
      this.utilization.set(utilization);
      this.eventTypes.set(eventTypes);
      this.maintenanceTrend.set(maintenanceTrend);
      this.revenueForecast.set(revenueForecast);
    } catch {
      this.error.set(this.locale.t('admin.analytics.loadError'));
    } finally {
      this.loading.set(false);
    }
  }

  protected readonly dateOptions: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short', year: 'numeric' };

  protected barHeight(value: number, max: number): number {
    return max <= 0 ? 0 : Math.max(2, (value / max) * 100);
  }

  /**
   * Reuses LocaleStore.intlLocale — the same `-u-nu-latn` tag it applies to
   * dates, so Arabic renders Western digits for money too, the convention
   * Gulf business apps use.
   */
  protected formatMoney(value: number): string {
    return new Intl.NumberFormat(this.locale.intlLocale(), { maximumFractionDigits: 0 }).format(value);
  }

  protected openMaintenance(carId: string): void {
    void this.router.navigate(['/admin/fleet', carId, 'maintenance']);
  }
}
