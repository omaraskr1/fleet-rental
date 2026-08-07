import { Component, computed, input, output, signal } from '@angular/core';
import { IonButton, IonIcon } from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { chevronBackOutline, chevronForwardOutline } from 'ionicons/icons';

import type { IsoDate } from '../core/models';

interface DayCell {
  date: IsoDate;
  dayOfMonth: number;
  state: 'booked' | 'pending' | 'open' | 'past';
}

/**
 * Month calendar showing booked vs open dates (feature 2), and reused by the
 * admin fleet view (feature 4).
 *
 * Takes booked/pending days as Sets so cell lookup is O(1) — a 90-day window
 * across a 20-car fleet would otherwise be a nested scan on every render.
 */
@Component({
  selector: 'app-availability-calendar',
  imports: [IonButton, IonIcon],
  template: `
    <div class="head">
      <ion-button fill="clear" size="small" (click)="shiftMonth(-1)" [disabled]="!canGoBack()">
        <ion-icon slot="icon-only" name="chevron-back-outline" />
      </ion-button>
      <strong>{{ monthLabel() }}</strong>
      <ion-button fill="clear" size="small" (click)="shiftMonth(1)">
        <ion-icon slot="icon-only" name="chevron-forward-outline" />
      </ion-button>
    </div>

    <div class="grid weekdays">
      @for (day of weekdays; track day) {
        <span>{{ day }}</span>
      }
    </div>

    <div class="grid">
      @for (blank of leadingBlanks(); track $index) {
        <span class="cell blank"></span>
      }
      @for (cell of cells(); track cell.date) {
        <button
          type="button"
          class="cell"
          [class.booked]="cell.state === 'booked'"
          [class.pending]="cell.state === 'pending'"
          [class.past]="cell.state === 'past'"
          [class.selected]="isSelected(cell.date)"
          [class.in-range]="isInRange(cell.date)"
          [disabled]="cell.state === 'booked' || cell.state === 'past' || !selectable()"
          (click)="dateClicked.emit(cell.date)">
          {{ cell.dayOfMonth }}
        </button>
      }
    </div>

    <div class="legend">
      <span><i class="swatch open"></i> Open</span>
      <span><i class="swatch pending"></i> Requested</span>
      <span><i class="swatch booked"></i> Booked</span>
    </div>
  `,
  styles: `
    .head { display: flex; align-items: center; justify-content: space-between; padding: 4px 0; }
    .grid { display: grid; grid-template-columns: repeat(7, 1fr); gap: 4px; }
    .weekdays { margin-bottom: 4px; }
    .weekdays span { text-align: center; font-size: 0.72rem; text-transform: uppercase;
                     color: var(--ion-color-medium); }
    .cell { aspect-ratio: 1; display: flex; align-items: center; justify-content: center;
            border: none; border-radius: 8px; font-size: 0.85rem;
            background: var(--ion-color-light); color: var(--ion-text-color); cursor: pointer; }
    .cell.blank { background: transparent; }
    .cell.past { opacity: 0.3; cursor: default; }
    .cell.pending { background: var(--ion-color-warning-tint); color: var(--ion-color-warning-contrast); }
    .cell.booked { background: var(--ion-color-danger-tint);
                   color: var(--ion-color-danger-contrast); text-decoration: line-through;
                   cursor: not-allowed; }
    .cell.in-range { background: var(--ion-color-primary-tint); }
    .cell.selected { background: var(--ion-color-primary); color: #fff; font-weight: 700; }
    .legend { display: flex; gap: 14px; justify-content: center; margin-top: 12px;
              font-size: 0.75rem; color: var(--ion-color-medium); }
    .legend span { display: flex; align-items: center; gap: 4px; }
    .swatch { width: 10px; height: 10px; border-radius: 3px; display: inline-block; }
    .swatch.open { background: var(--ion-color-light); border: 1px solid var(--ion-color-medium); }
    .swatch.pending { background: var(--ion-color-warning-tint); }
    .swatch.booked { background: var(--ion-color-danger-tint); }
  `,
})
export class AvailabilityCalendarComponent {
  readonly bookedDates = input<Set<IsoDate>>(new Set());
  readonly pendingDates = input<Set<IsoDate>>(new Set());
  readonly selectable = input(false);
  readonly rangeStart = input<IsoDate | null>(null);
  readonly rangeEnd = input<IsoDate | null>(null);

  readonly dateClicked = output<IsoDate>();

  /** Months offset from the current month; 0 is the month being viewed now. */
  private readonly offset = signal(0);

  private readonly viewDate = computed(() => {
    const now = new Date();
    return new Date(now.getFullYear(), now.getMonth() + this.offset(), 1);
  });

  protected readonly weekdays = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];

  protected readonly monthLabel = computed(() =>
    this.viewDate().toLocaleDateString(undefined, { month: 'long', year: 'numeric' }),
  );

  /** Never let the user page back before the current month. */
  protected readonly canGoBack = computed(() => this.offset() > 0);

  /** Blank cells so the 1st lands on the right weekday, with Monday first. */
  protected readonly leadingBlanks = computed(() => {
    const firstDay = this.viewDate().getDay(); // 0 = Sunday
    const mondayFirst = (firstDay + 6) % 7;
    return Array.from({ length: mondayFirst });
  });

  protected readonly cells = computed<DayCell[]>(() => {
    const view = this.viewDate();
    const year = view.getFullYear();
    const month = view.getMonth();
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const today = toIso(new Date());
    const booked = this.bookedDates();
    const pending = this.pendingDates();

    return Array.from({ length: daysInMonth }, (_, i) => {
      const date = toIso(new Date(year, month, i + 1));

      // Booked wins over past so a held date still reads as held.
      const state: DayCell['state'] = booked.has(date)
        ? 'booked'
        : date < today
          ? 'past'
          : pending.has(date)
            ? 'pending'
            : 'open';

      return { date, dayOfMonth: i + 1, state };
    });
  });

  constructor() {
    addIcons({ chevronBackOutline, chevronForwardOutline });
  }

  protected shiftMonth(delta: number): void {
    this.offset.update((value) => Math.max(0, value + delta));
  }

  protected isSelected(date: IsoDate): boolean {
    return date === this.rangeStart() || date === this.rangeEnd();
  }

  protected isInRange(date: IsoDate): boolean {
    const start = this.rangeStart();
    const end = this.rangeEnd();
    return start !== null && end !== null && date > start && date < end;
  }
}

/** Local date -> "YYYY-MM-DD" without the UTC shift toISOString would introduce. */
export function toIso(date: Date): IsoDate {
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}
