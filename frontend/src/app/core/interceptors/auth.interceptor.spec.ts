import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { authInterceptor } from './auth.interceptor';
import { AuthStore } from '../stores/auth.store';
import { ApiService } from '../services/api.service';
import type { AppUser } from '../models';

const USER: AppUser = {
  id: 'u1',
  email: 'c@test.com',
  fullName: 'Client',
  phoneNumber: null,
  role: 'Client',
};

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let auth: AuthStore;
  let api: { login: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    localStorage.clear();
    api = { login: vi.fn() };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: ApiService, useValue: api },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthStore);
  });

  afterEach(() => httpMock.verify());

  it('attaches no Authorization header when signed out', () => {
    http.get('/api/cars').subscribe();

    const req = httpMock.expectOne('/api/cars');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush([]);
  });

  it('attaches the bearer token from the store once signed in', async () => {
    api.login.mockResolvedValue({ accessToken: 'live-token', expiresAt: '', user: USER });
    await auth.login('c@test.com', 'pw');

    http.get('/api/bookings/mine').subscribe();

    const req = httpMock.expectOne('/api/bookings/mine');
    expect(req.request.headers.get('Authorization')).toBe('Bearer live-token');
    req.flush([]);
  });

  it('a sign-out mid-session takes effect immediately, without a page reload', async () => {
    api.login.mockResolvedValue({ accessToken: 'live-token', expiresAt: '', user: USER });
    await auth.login('c@test.com', 'pw');
    auth.signOut();

    http.get('/api/cars').subscribe();

    const req = httpMock.expectOne('/api/cars');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush([]);
  });
});
