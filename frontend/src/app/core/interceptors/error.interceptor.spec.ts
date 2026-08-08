import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { errorInterceptor } from './error.interceptor';
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

describe('errorInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let auth: AuthStore;
  let router: Router;
  let api: { login: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    localStorage.clear();
    api = { login: vi.fn() };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ApiService, useValue: api },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthStore);
    router = TestBed.inject(Router);
  });

  afterEach(() => httpMock.verify());

  async function expectMessage(setup: () => void, expected: string): Promise<void> {
    const promise = firstValueFrom(http.get('/api/whatever'));
    setup();

    await expect(promise).rejects.toThrow(expected);
  }

  it('a network failure (status 0) gets a connectivity message, not a raw error', async () => {
    await expectMessage(
      () => httpMock.expectOne('/api/whatever').error(new ProgressEvent('error'), { status: 0 }),
      'Cannot reach the server',
    );
  });

  it('field validation errors are joined and take priority over the generic title', async () => {
    await expectMessage(
      () =>
        httpMock.expectOne('/api/whatever').flush(
          {
            status: 400,
            title: 'Validation failed',
            detail: 'One or more validation errors occurred.',
            errors: { Email: ['A valid email address is required.'] },
          },
          { status: 400, statusText: 'Bad Request' },
        ),
      'A valid email address is required.',
    );
  });

  it('falls back to the problem detail when there are no field errors', async () => {
    await expectMessage(
      () =>
        httpMock.expectOne('/api/whatever').flush(
          { status: 409, title: 'Conflict', detail: 'Already booked for part of this range.' },
          { status: 409, statusText: 'Conflict' },
        ),
      'Already booked for part of this range.',
    );
  });

  it('a 500 with no usable body gets a generic server-side message', async () => {
    await expectMessage(
      () => httpMock.expectOne('/api/whatever').flush(null, { status: 500, statusText: 'Error' }),
      'Something went wrong on our end',
    );
  });

  it('a 400 with no usable body gets a generic client-side message, distinct from the 500 case', async () => {
    await expectMessage(
      () => httpMock.expectOne('/api/whatever').flush(null, { status: 400, statusText: 'Bad Request' }),
      'could not be completed',
    );
  });

  describe('401 handling', () => {
    it('signs the user out and redirects to login on a 401 from a protected endpoint', async () => {
      api.login.mockResolvedValue({ accessToken: 'tok', expiresAt: '', user: USER });
      await auth.login('c@test.com', 'pw');

      const navigateSpy = vi.spyOn(router, 'navigate');
      const promise = firstValueFrom(http.get('/api/bookings/mine'));

      httpMock
        .expectOne('/api/bookings/mine')
        .flush(null, { status: 401, statusText: 'Unauthorized' });

      await expect(promise).rejects.toThrow();

      expect(auth.isAuthenticated()).toBe(false);
      expect(navigateSpy).toHaveBeenCalledWith(['/login']);
    });

    it('does NOT sign out on a 401 from the login call itself, which would be a busy-loop', async () => {
      const navigateSpy = vi.spyOn(router, 'navigate');
      const promise = firstValueFrom(http.get('/api/auth/login'));

      httpMock.expectOne('/api/auth/login').flush(null, { status: 401, statusText: 'Unauthorized' });

      await expect(promise).rejects.toThrow();

      expect(navigateSpy).not.toHaveBeenCalled();
    });
  });
});
