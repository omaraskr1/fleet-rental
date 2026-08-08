import { Injectable, inject, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';

import { ApiService } from '../services/api.service';
import { AuthStore } from './auth.store';
import { environment } from '../../../environments/environment';
import type { CarLocation } from '../models';

/**
 * Live fleet locations for the owner's map (feature 6). Loads the latest known
 * reading per car once, then keeps it current via a SignalR connection to
 * `LocationHub` — the first realtime piece in this app; everything else here is
 * plain request/response.
 */
@Injectable({ providedIn: 'root' })
export class LocationsStore {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthStore);

  private readonly _locations = signal<Map<string, CarLocation>>(new Map());
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);
  private readonly _connected = signal(false);

  readonly locations = this._locations.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly connected = this._connected.asReadonly();

  private connection: signalR.HubConnection | null = null;

  async connect(): Promise<void> {
    this._loading.set(true);
    this._error.set(null);

    try {
      const readings = await this.api.getCarLocations();
      this._locations.set(new Map(readings.map((r) => [r.carId, r])));
    } catch (error) {
      this._error.set((error as Error).message);
    } finally {
      this._loading.set(false);
    }

    if (this.connection) {
      return;
    }

    // Hubs live outside /api — the REST base with that suffix stripped.
    const hubUrl = `${environment.apiBaseUrl.replace(/\/api$/, '')}/hubs/locations`;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, { accessTokenFactory: () => this.auth.token() ?? '' })
      .withAutomaticReconnect()
      .build();

    this.connection.on('locationUpdated', (update: CarLocation) => {
      const next = new Map(this._locations());
      next.set(update.carId, update);
      this._locations.set(next);
    });

    this.connection.onreconnected(() => this._connected.set(true));
    this.connection.onclose(() => this._connected.set(false));

    try {
      await this.connection.start();
      this._connected.set(true);
    } catch (error) {
      this._error.set((error as Error).message);
    }
  }

  async disconnect(): Promise<void> {
    await this.connection?.stop();
    this.connection = null;
    this._connected.set(false);
  }
}
