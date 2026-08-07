import { Component, computed, inject, OnInit } from '@angular/core';
import { IonNote, IonSpinner } from '@ionic/angular/standalone';

import { BookingsStore } from '../../core/stores/bookings.store';
import { toIso } from '../../shared/availability-calendar.component';

interface LaneDay {
  date: string;
  label: number;
  state: 'booked' | 'pending' | 'open';
  isWeekend: boolean;
}

/**
 * Feature 4 — one lane per car across a 30-day window, so the fleet owner sees
 * every commitment at once and can spot the free vehicle for a new enquiry.
 */
@Component({
  selector: 'app-admin-calendar',
  imports: [IonSpinner, IonNote],
  template: `
    <h1>Fleet calendar</h1>
    <ion-note>Next 30 days across every vehicle.</ion-note>

    @if (store.loading() && !store.fleet()) {
      <div class="state"><ion-spinner /></div>
    } @else if (store.fleet(); as fleet) {
      <div class="scroller">
        <table>
          <thead>
            <tr>
              <th class="car">Vehicle</th>
              @for (day of days(); track day) {
                <th class="day" [class.weekend]="isWeekend(day)">{{ dayLabel(day) }}</th>
              }
            </tr>
          </thead>
          <tbody>
            @for (lane of lanes(); track lane.carId) {
              <tr>
                <td class="car">{{ lane.carName }}</td>
                @for (cell of lane.cells; track cell.date) {
                  <td
                    class="cell"
                    [class.booked]="cell.state === 'booked'"
                    [class.pending]="cell.state === 'pending'"
                    [class.weekend]="cell.isWeekend"
                    [title]="cell.date + ' — ' + cell.state"></td>
                }
              </tr>
            }
          </tbody>
        </table>
      </div>

      <div class="legend">
        <span><i class="swatch open"></i> Open</span>
        <span><i class="swatch pending"></i> Requested</span>
        <span><i class="swatch booked"></i> Booked</span>
      </div>
    }
  `,
  styles: `
    h1 { font-size: 1.4rem; margin: 0 0 4px; }
    /* Wide content must scroll inside its own container, never the page body. */
    .scroller { overflow-x: auto; margin-top: 20px; }
    table { border-collapse: collapse; font-size: 0.75rem; }
    th.car, td.car { position: sticky; left: 0; background: var(--ion-background-color);
                     text-align: left; padding-right: 12px; white-space: nowrap;
                     min-width: 150px; font-weight: 500; z-index: 1; }
    th.day { font-weight: 400; color: var(--ion-color-medium); padding-bottom: 6px;
             min-width: 22px; }
    th.weekend { color: var(--ion-color-primary); }
    .cell { width: 22px; height: 26px; border: 1px solid var(--ion-background-color);
            background: var(--ion-color-light); }
    .cell.weekend { background: var(--ion-color-light-shade); }
    .cell.pending { background: var(--ion-color-warning-tint); }
    .cell.booked { background: var(--ion-color-danger-tint); }
    .legend { display: flex; gap: 16px; margin-top: 16px; font-size: 0.78rem;
              color: var(--ion-color-medium); }
    .legend span { display: flex; align-items: center; gap: 5px; }
    .swatch { width: 11px; height: 11px; border-radius: 3px; display: inline-block; }
    .swatch.open { background: var(--ion-color-light); border: 1px solid var(--ion-color-medium); }
    .swatch.pending { background: var(--ion-color-warning-tint); }
    .swatch.booked { background: var(--ion-color-danger-tint); }
    .state { padding: 48px; text-align: center; }
  `,
})
export class AdminCalendarPage implements OnInit {
  protected readonly store = inject(BookingsStore);

  private static readonly WINDOW_DAYS = 30;

  protected readonly days = computed(() => {
    const start = new Date();
    return Array.from({ length: AdminCalendarPage.WINDOW_DAYS }, (_, i) => {
      const date = new Date(start);
      date.setDate(start.getDate() + i);
      return toIso(date);
    });
  });

  protected readonly lanes = computed(() => {
    const fleet = this.store.fleet();
    if (!fleet) return [];

    const days = this.days();

    return fleet.cars.map((car) => {
      // Sets, so each lane is a linear pass rather than a scan per cell.
      const booked = new Set(car.bookedDates);
      const pending = new Set(car.pendingDates);

      const cells: LaneDay[] = days.map((date) => ({
        date,
        label: Number(date.slice(8)),
        state: booked.has(date) ? 'booked' : pending.has(date) ? 'pending' : 'open',
        isWeekend: this.isWeekend(date),
      }));

      return { carId: car.carId, carName: car.carName, cells };
    });
  });

  ngOnInit(): void {
    const days = this.days();
    void this.store.loadFleetAvailability(days[0], days[days.length - 1]);
  }

  protected dayLabel(date: string): string {
    return date.slice(8);
  }

  protected isWeekend(date: string): boolean {
    const day = new Date(`${date}T00:00:00`).getDay();
    return day === 0 || day === 6;
  }
}
