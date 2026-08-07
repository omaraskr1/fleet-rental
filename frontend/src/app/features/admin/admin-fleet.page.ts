import { Component, inject, OnInit } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { IonBadge, IonNote, IonSpinner } from '@ionic/angular/standalone';

import { CarsStore } from '../../core/stores/cars.store';
import { humanize } from '../../core/models';

/**
 * Read-only fleet overview. Vehicle create/edit is deliberately out of Phase 1
 * scope — the API endpoints exist (POST/PUT /api/cars) and are admin-guarded,
 * so the management UI is additive rather than a rewrite.
 */
@Component({
  selector: 'app-admin-fleet',
  imports: [IonBadge, IonSpinner, IonNote, CurrencyPipe],
  template: `
    <h1>Vehicles</h1>
    <ion-note>{{ store.availableCount() }} of {{ store.cars().length }} free today.</ion-note>

    @if (store.loading() && store.cars().length === 0) {
      <div class="state"><ion-spinner /></div>
    } @else {
      <div class="scroller">
        <table>
          <thead>
            <tr>
              <th>Vehicle</th><th>Category</th><th>Seats</th>
              <th>Daily rate</th><th>Status</th><th>Today</th>
            </tr>
          </thead>
          <tbody>
            @for (car of store.cars(); track car.id) {
              <tr>
                <td class="name">{{ car.name }}</td>
                <td>{{ label(car.category) }}</td>
                <td>{{ car.seats }}</td>
                <td>{{ car.dailyRate | currency: 'USD' : 'symbol' : '1.0-0' }}</td>
                <td>
                  <ion-badge [color]="car.status === 'Active' ? 'success' : 'medium'">
                    {{ label(car.status) }}
                  </ion-badge>
                </td>
                <td>
                  <ion-badge [color]="car.availableToday ? 'success' : 'warning'">
                    {{ car.availableToday ? 'Free' : 'Booked' }}
                  </ion-badge>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: `
    h1 { font-size: 1.4rem; margin: 0 0 4px; }
    .scroller { overflow-x: auto; margin-top: 20px; }
    table { border-collapse: collapse; width: 100%; font-size: 0.9rem; }
    th { text-align: left; font-weight: 500; color: var(--ion-color-medium);
         font-size: 0.75rem; text-transform: uppercase; padding: 8px 12px 8px 0; }
    td { padding: 10px 12px 10px 0; border-top: 1px solid var(--ion-color-light-shade);
         white-space: nowrap; }
    td.name { font-weight: 500; }
    .state { padding: 48px; text-align: center; }
  `,
})
export class AdminFleetPage implements OnInit {
  protected readonly store = inject(CarsStore);

  ngOnInit(): void {
    void this.store.loadCars();
  }

  protected label(value: string): string {
    return humanize(value);
  }
}
