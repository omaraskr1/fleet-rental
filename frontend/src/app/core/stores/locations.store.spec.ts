import { TestBed } from '@angular/core/testing';

import { LocationsStore } from './locations.store';
import { ApiService } from '../services/api.service';
import { AuthStore } from './auth.store';
import type { CarLocation } from '../models';

// A minimal fake standing in for signalR.HubConnection — captures the handler
// registered via .on('locationUpdated', ...) so the test can invoke it
// directly, simulating a message pushed from the hub.
class FakeHubConnection {
  handlers = new Map<string, (payload: unknown) => void>();
  started = false;

  on(event: string, handler: (payload: unknown) => void): void {
    this.handlers.set(event, handler);
  }

  onreconnected(): void {}

  onclose(): void {}

  async start(): Promise<void> {
    this.started = true;
  }

  async stop(): Promise<void> {
    this.started = false;
  }

  emit(event: string, payload: unknown): void {
    this.handlers.get(event)?.(payload);
  }
}

let lastConnection: FakeHubConnection;

vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: class {
    withUrl() {
      return this;
    }
    withAutomaticReconnect() {
      return this;
    }
    build() {
      lastConnection = new FakeHubConnection();
      return lastConnection;
    }
  },
}));

function reading(overrides: Partial<CarLocation> = {}): CarLocation {
  return {
    carId: 'c1',
    carName: 'Test Car',
    latitude: 25.2,
    longitude: 55.3,
    recordedAt: '2026-08-08T12:00:00Z',
    ...overrides,
  };
}

describe('LocationsStore', () => {
  let api: { getCarLocations: ReturnType<typeof vi.fn> };
  let store: LocationsStore;

  beforeEach(() => {
    api = { getCarLocations: vi.fn().mockResolvedValue([]) };
    TestBed.configureTestingModule({
      providers: [
        { provide: ApiService, useValue: api },
        { provide: AuthStore, useValue: { token: () => 'test-token' } },
      ],
    });
    store = TestBed.inject(LocationsStore);
  });

  it('loads the initial fetch into the location map', async () => {
    api.getCarLocations.mockResolvedValue([reading({ carId: 'c1' }), reading({ carId: 'c2' })]);

    await store.connect();

    expect(store.locations().size).toBe(2);
    expect(store.locations().get('c1')?.carId).toBe('c1');
    expect(store.loading()).toBe(false);
  });

  it('connects the hub and marks itself connected', async () => {
    await store.connect();

    expect(store.connected()).toBe(true);
    expect(lastConnection.started).toBe(true);
  });

  it('a locationUpdated push updates or adds the reading for that car live', async () => {
    api.getCarLocations.mockResolvedValue([reading({ carId: 'c1', latitude: 25.2 })]);
    await store.connect();

    lastConnection.emit('locationUpdated', reading({ carId: 'c1', latitude: 26.5 }));

    expect(store.locations().get('c1')?.latitude).toBe(26.5);

    lastConnection.emit('locationUpdated', reading({ carId: 'c2', latitude: 30.1 }));

    expect(store.locations().size).toBe(2);
    expect(store.locations().get('c2')?.latitude).toBe(30.1);
  });

  it('a failed initial fetch surfaces the error but still attempts the hub connection', async () => {
    api.getCarLocations.mockRejectedValue(new Error('Cannot reach the server.'));

    await store.connect();

    expect(store.error()).toBe('Cannot reach the server.');
    expect(lastConnection.started).toBe(true);
  });

  it('disconnect stops the hub and clears connected state', async () => {
    await store.connect();
    expect(store.connected()).toBe(true);

    await store.disconnect();

    expect(store.connected()).toBe(false);
    expect(lastConnection.started).toBe(false);
  });
});
