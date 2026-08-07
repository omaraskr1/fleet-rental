import { Routes } from '@angular/router';

import { adminGuard, authGuard, guestGuard } from './core/guards/auth.guard';

/**
 * Every screen is lazily loaded. On mobile this keeps the initial bundle to the
 * car list and login; the admin section in particular never reaches a phone.
 */
export const routes: Routes = [
  { path: '', redirectTo: 'tabs/cars', pathMatch: 'full' },

  // ---------- Client auth ----------
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login.page').then((m) => m.LoginPage),
  },
  {
    path: 'signup',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/signup.page').then((m) => m.SignupPage),
  },

  // ---------- Client app (tabbed shell) ----------
  {
    path: 'tabs',
    loadComponent: () => import('./features/tabs/tabs.page').then((m) => m.TabsPage),
    children: [
      { path: '', redirectTo: 'cars', pathMatch: 'full' },
      {
        path: 'cars',
        loadComponent: () => import('./features/cars/car-list.page').then((m) => m.CarListPage),
      },
      {
        path: 'bookings',
        canActivate: [authGuard],
        loadComponent: () =>
          import('./features/bookings/my-bookings.page').then((m) => m.MyBookingsPage),
      },
      {
        path: 'profile',
        canActivate: [authGuard],
        loadComponent: () => import('./features/profile/profile.page').then((m) => m.ProfilePage),
      },
    ],
  },

  // Full-screen routes, outside the tab bar so the calendar and form get the
  // whole viewport on a phone.
  {
    path: 'cars/:id',
    loadComponent: () => import('./features/cars/car-detail.page').then((m) => m.CarDetailPage),
  },
  {
    path: 'cars/:id/book',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/bookings/booking-form.page').then((m) => m.BookingFormPage),
  },
  {
    path: 'bookings/:id',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/bookings/booking-detail.page').then((m) => m.BookingDetailPage),
  },

  // ---------- Admin panel (web) ----------
  {
    path: 'admin/login',
    loadComponent: () =>
      import('./features/admin/admin-login.page').then((m) => m.AdminLoginPage),
  },
  {
    path: 'admin',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/admin/admin-shell.page').then((m) => m.AdminShellPage),
    children: [
      { path: '', redirectTo: 'requests', pathMatch: 'full' },
      {
        path: 'requests',
        loadComponent: () =>
          import('./features/admin/admin-requests.page').then((m) => m.AdminRequestsPage),
      },
      {
        path: 'calendar',
        loadComponent: () =>
          import('./features/admin/admin-calendar.page').then((m) => m.AdminCalendarPage),
      },
      {
        path: 'fleet',
        loadComponent: () => import('./features/admin/admin-fleet.page').then((m) => m.AdminFleetPage),
      },
    ],
  },

  { path: '**', redirectTo: 'tabs/cars' },
];
