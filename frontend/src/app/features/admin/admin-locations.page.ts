import {
  AfterViewInit, Component, DestroyRef, ElementRef, OnDestroy, OnInit, effect, inject, viewChild,
} from '@angular/core';
import * as L from 'leaflet';
import { IonBadge, IonNote, IonSpinner } from '@ionic/angular/standalone';

import { LocationsStore } from '../../core/stores/locations.store';
import { LocaleStore } from '../../core/stores/locale.store';

/**
 * Live map of the fleet (feature 6). One marker per car with a known reading,
 * moving as new pings arrive over the SignalR connection `LocationsStore` owns
 * — no polling, no manual refresh needed while this page is open.
 */
@Component({
  selector: 'app-admin-locations',
  imports: [IonBadge, IonNote, IonSpinner],
  template: `
    <div class="header">
      <h1>{{ locale.t('admin.locations.title') }}</h1>
      <ion-badge [color]="store.connected() ? 'success' : 'medium'">
        {{ store.connected() ? locale.t('admin.locations.live') : locale.t('admin.locations.connecting') }}
      </ion-badge>
    </div>

    @if (store.error(); as error) {
      <ion-note color="danger" class="banner">{{ error }}</ion-note>
    }

    @if (store.loading()) {
      <div class="state"><ion-spinner /></div>
    }

    <div #mapContainer class="map"></div>

    @if (!store.loading() && store.locations().size === 0) {
      <ion-note class="empty">{{ locale.t('admin.locations.empty') }}</ion-note>
    }
  `,
  styles: `
    .header { display: flex; align-items: center; gap: 12px; margin-bottom: 16px; }
    h1 { font-size: 1.4rem; margin: 0; }
    .map { height: 60vh; min-height: 360px; border-radius: 8px; overflow: hidden;
           border: 1px solid var(--ion-color-light-shade); }
    .state { padding: 24px; text-align: center; }
    .empty { display: block; margin-top: 12px; }
    .banner { display: block; padding: 8px 0; white-space: pre-line; }
  `,
})
export class AdminLocationsPage implements OnInit, AfterViewInit, OnDestroy {
  protected readonly store = inject(LocationsStore);
  protected readonly locale = inject(LocaleStore);
  private readonly destroyRef = inject(DestroyRef);

  private readonly mapContainer = viewChild.required<ElementRef<HTMLDivElement>>('mapContainer');

  private map: L.Map | null = null;
  private readonly markers = new Map<string, L.Marker>();

  constructor() {
    // Runs whenever the location map changes — including every live update —
    // and is a no-op until ngAfterViewInit has created the Leaflet map.
    effect(() => {
      const readings = this.store.locations();

      if (!this.map) {
        return;
      }

      for (const reading of readings.values()) {
        const existing = this.markers.get(reading.carId);

        if (existing) {
          existing.setLatLng([reading.latitude, reading.longitude]);
        } else {
          const marker = L.marker([reading.latitude, reading.longitude]).addTo(this.map);
          marker.bindPopup(reading.carName);
          this.markers.set(reading.carId, marker);
        }

        this.markers.get(reading.carId)!.setPopupContent(reading.carName);
      }
    });

    this.destroyRef.onDestroy(() => void this.store.disconnect());
  }

  ngOnInit(): void {
    void this.store.connect();
  }

  ngAfterViewInit(): void {
    // Dubai — a reasonable default center for this fleet until real readings
    // arrive and the view can be fit to them instead.
    this.map = L.map(this.mapContainer().nativeElement).setView([25.2048, 55.2708], 11);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; OpenStreetMap contributors',
      maxZoom: 19,
    }).addTo(this.map);

    const existing = [...this.store.locations().values()];
    if (existing.length > 0) {
      const bounds = L.latLngBounds(existing.map((r) => [r.latitude, r.longitude]));
      this.map.fitBounds(bounds, { padding: [40, 40], maxZoom: 14 });
    }
  }

  ngOnDestroy(): void {
    this.map?.remove();
    this.map = null;
  }
}
