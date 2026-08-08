import { Routes } from '@angular/router';

import { adminGuard, authGuard, guestGuard } from './core/guards/auth.guard';
import { analyticsFeatureGuard, gpsFeatureGuard, maintenanceFeatureGuard } from './core/guards/feature.guard';
import { platformAuthGuard, platformGuestGuard } from './core/guards/platform-auth.guard';
import { tenantGuard } from './core/guards/tenant.guard';

/**
 * Every screen is lazily loaded. On mobile this keeps the initial bundle to the
 * car list and login; the admin section in particular never reaches a phone.
 *
 * tenantGuard sits ahead of every route except company selection itself — WHO you
 * are is meaningless until WHICH rental company has been established, and every
 * API call needs that answer before it can be scoped correctly.
 */
export const routes: Routes = [
  { path: '', redirectTo: 'tabs/cars', pathMatch: 'full' },

  {
    path: 'select-company',
    loadComponent: () =>
      import('./features/tenant/select-company.page').then((m) => m.SelectCompanyPage),
  },

  // ---------- Client auth ----------
  {
    path: 'login',
    canActivate: [tenantGuard, guestGuard],
    loadComponent: () => import('./features/auth/login.page').then((m) => m.LoginPage),
  },
  {
    path: 'signup',
    canActivate: [tenantGuard, guestGuard],
    loadComponent: () => import('./features/auth/signup.page').then((m) => m.SignupPage),
  },

  // ---------- Client app (tabbed shell) ----------
  {
    path: 'tabs',
    canActivate: [tenantGuard],
    loadComponent: () => import('./features/tabs/tabs.page').then((m) => m.TabsPage),
    children: [
      { path: '', redirectTo: 'cars', pathMatch: 'full' },
      {
        path: 'cars',
        canActivate: [authGuard],
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
    canActivate: [tenantGuard, authGuard],
    loadComponent: () => import('./features/cars/car-detail.page').then((m) => m.CarDetailPage),
  },
  {
    path: 'cars/:id/book',
    canActivate: [tenantGuard, authGuard],
    loadComponent: () =>
      import('./features/bookings/booking-form.page').then((m) => m.BookingFormPage),
  },
  {
    path: 'bookings/:id',
    canActivate: [tenantGuard, authGuard],
    loadComponent: () =>
      import('./features/bookings/booking-detail.page').then((m) => m.BookingDetailPage),
  },

  // ---------- Admin panel (web) ----------
  // The web admin panel is used by one operator per browser session, who knows
  // their own company's URL, so it also goes through company selection rather
  // than assuming a single hardcoded tenant.
  {
    path: 'admin/login',
    canActivate: [tenantGuard],
    loadComponent: () =>
      import('./features/admin/admin-login.page').then((m) => m.AdminLoginPage),
  },
  {
    path: 'admin',
    canActivate: [tenantGuard, adminGuard],
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
      {
        path: 'fleet/new',
        loadComponent: () =>
          import('./features/admin/admin-car-form.page').then((m) => m.AdminCarFormPage),
      },
      {
        path: 'fleet/:carId/edit',
        loadComponent: () =>
          import('./features/admin/admin-car-form.page').then((m) => m.AdminCarFormPage),
      },
      {
        path: 'fleet/:carId/maintenance',
        canActivate: [maintenanceFeatureGuard],
        loadComponent: () =>
          import('./features/admin/admin-car-maintenance.page').then((m) => m.AdminCarMaintenancePage),
      },
      {
        path: 'issues',
        canActivate: [maintenanceFeatureGuard],
        loadComponent: () =>
          import('./features/admin/admin-issues.page').then((m) => m.AdminIssuesPage),
      },
      {
        path: 'services',
        canActivate: [maintenanceFeatureGuard],
        loadComponent: () =>
          import('./features/admin/admin-service-catalog.page').then((m) => m.AdminServiceCatalogPage),
      },
      {
        path: 'analytics',
        canActivate: [analyticsFeatureGuard],
        loadComponent: () =>
          import('./features/admin/admin-analytics.page').then((m) => m.AdminAnalyticsPage),
      },
      {
        path: 'locations',
        canActivate: [gpsFeatureGuard],
        loadComponent: () =>
          import('./features/admin/admin-locations.page').then((m) => m.AdminLocationsPage),
      },
    ],
  },

  // ---------- Platform panel (super-admin, across every company) ----------
  // Deliberately outside the tenant route tree — no tenantGuard here. A
  // platform admin operates above/across companies, not inside one, so
  // "which company" is not a question these routes ever need answered.
  {
    path: 'platform/login',
    canActivate: [platformGuestGuard],
    loadComponent: () =>
      import('./features/platform/platform-login.page').then((m) => m.PlatformLoginPage),
  },
  {
    path: 'platform',
    canActivate: [platformAuthGuard],
    loadComponent: () =>
      import('./features/platform/platform-shell.page').then((m) => m.PlatformShellPage),
    children: [
      { path: '', redirectTo: 'companies', pathMatch: 'full' },
      {
        path: 'companies',
        loadComponent: () =>
          import('./features/platform/platform-companies.page').then((m) => m.PlatformCompaniesPage),
      },
      {
        path: 'companies/:tenantId/admins',
        loadComponent: () =>
          import('./features/platform/platform-company-admins.page').then(
            (m) => m.PlatformCompanyAdminsPage,
          ),
      },
      {
        path: 'companies/:tenantId/features',
        loadComponent: () =>
          import('./features/platform/platform-company-features.page').then(
            (m) => m.PlatformCompanyFeaturesPage,
          ),
      },
      {
        path: 'cars',
        loadComponent: () =>
          import('./features/platform/platform-cars.page').then((m) => m.PlatformCarsPage),
      },
    ],
  },

  { path: '**', redirectTo: 'tabs/cars' },
];
