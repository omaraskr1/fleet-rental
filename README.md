# Fleet Rental

Booking platform for a small fleet of marketing/event rental cars. Fleet owners
list vehicles; clients browse availability and submit booking requests for
specific dates and events.

One Angular codebase produces three targets:

| Target | Built with | Output |
|---|---|---|
| iOS app | Capacitor → Xcode | `.ipa` for the App Store |
| Android app | Capacitor → Gradle | `.aab` for Play |
| Admin panel | `ng build` | static web bundle |

---

## Phase 1 status

All six MVP features are implemented and verified end to end.

| # | Feature | Where |
|---|---|---|
| 1 | Car listing — photo, name, category, availability | `features/cars/car-list.page.ts` |
| 2 | Per-car availability calendar | `shared/availability-calendar.component.ts` |
| 3 | Booking request form with event details | `features/bookings/booking-form.page.ts` |
| 4 | Admin panel — queue, approve/reject, fleet calendar | `features/admin/` |
| 5 | Client signup/login + separate admin login | `features/auth/`, `features/admin/admin-login.page.ts` |
| 6 | Notification on approve/reject | `Application/Notifications/`, `Infrastructure/Notifications/` |

---

## Multi-tenancy

The platform serves multiple rental businesses from one shared database. Each is
a `Tenant`; every car, booking, event, user and device belongs to exactly one.

- **Isolation is enforced by EF Core global query filters, applied by
  convention** to every entity implementing `ITenantOwned` — not by remembering
  a `WHERE TenantId = ...` in each query. A new entity that forgets to derive
  from `TenantEntity` fails `TenantIsolationTests.Every_persisted_entity_...` in
  CI rather than silently leaking.
- **Filters fail closed.** No tenant resolved means the filter matches nothing,
  not everything — an unknown or missing company code returns an empty result,
  never another tenant's data.
- **The tenant travels inside the signed JWT**, not a header. An authenticated
  caller cannot switch tenants by editing `X-Tenant-Code` — the middleware
  ignores that header entirely once a token is present. The header only decides
  which tenant an *anonymous* request (browsing, login) belongs to.
- **Email is unique per tenant, not platform-wide** — the same person can
  legitimately hold a client account at two different rental companies.
- **Clients pick their company once**, by code, on first launch
  (`select-company.page.ts`). The choice is remembered on the device; every
  request after that carries it automatically via `tenant.interceptor.ts`.

See `tests/FleetRental.IntegrationTests/TenantIsolationTests.cs` for the tests
that exercise all of this against real SQL Server, including the deliberate
attack case (an authenticated client trying to read another company's fleet by
changing the header).

---

## Localization and theming

Every screen in the client app and admin panel supports English and Arabic
with full RTL, plus a manual light/dark theme independent of the OS setting.

- **Runtime language switching**, not Angular's built-in compile-time i18n —
  a company code is entered once per install, so shipping a separate binary
  per language was never on the table. Translations live as plain JSON
  (`frontend/public/i18n/{en,ar}.json`, one flat key set, 148 keys) fetched and
  cached by `LocaleStore`. No i18n library dependency.
- **Dates use the native `Intl` API**, not Angular's `DatePipe` — DatePipe
  needs `@angular/common/locales/ar` registered against a single `LOCALE_ID`,
  which is a build-time, one-locale-per-build mechanism that doesn't fit a
  runtime switch. `LocaleStore.formatDate()` wraps `toLocaleDateString` with
  the `-u-nu-latn` Unicode extension, so Arabic renders with Arabic month
  names but Western numerals — the convention Gulf business apps use for
  dates and money.
- **RTL is mostly Ionic's own doing.** Setting `dir="rtl"` on `<html>` is the
  only line of framework wiring required; Ionic mirrors its own components
  automatically. The custom CSS in this app uses logical properties
  (`padding-inline-end`, `border-inline-end`, `text-align: start`) rather than
  `left`/`right` so it mirrors too, and the calendar's prev/next chevrons use
  `ion-icon`'s `flipRtl` input so "previous" still visually points the
  correct direction once flex has already reordered the buttons.
- **Theme uses `dark.class.css`, not `dark.system.css`** — the OS-media-query
  variant Ionic ships by default has no JS hook to override it.
  `ThemeStore` toggles the `ion-palette-dark` class on `<html>`, defaulting to
  the OS preference on first launch and tracking it live thereafter, until the
  user picks light or dark explicitly.
- **Backend error messages are not localized.** A validation message from the
  API (e.g. "Email is required") reaches the Arabic UI in English. Translating
  those means the API reading `Accept-Language` and maintaining a message
  catalog per exception — real scope, deliberately left for later rather than
  silently skipped.

A real defect surfaced during this work, not a hypothetical: `App.ngOnInit`
originally restored the session, tenant, and locale — but the router's
initial navigation could evaluate `tenantGuard`/`authGuard` before those
awaited restores finished, bouncing a returning signed-in user back to
"sign in" or "select your company" on a hard reload. Fixed by moving
restoration into a `provideAppInitializer` in `app.config.ts`, which blocks
bootstrap — and therefore the router's first navigation — until it resolves.
Verified by reloading directly on a deep, guarded route before and after the
fix.

---

## Repository layout

```
backend/
  src/
    FleetRental.Domain/          entities, enums, DateRange — no dependencies
    FleetRental.Application/     use cases, DTOs, abstractions
    FleetRental.Infrastructure/  EF Core, JWT, hashing, notification channels
    FleetRental.Api/             controllers, DI, error mapping
frontend/
  src/app/
    core/       models, api service, signal stores, guards, interceptors
    features/   one folder per screen, all lazily loaded
    shared/     availability calendar, status badge
  android/      generated Gradle project
  ios/          generated Xcode project
scripts/
  e2e-api-test.sh          full-flow API test, including double-booking
  reset-test-data.ps1      clears bookings so the suite is repeatable
```

### Where a new feature belongs

`FleetRental.Application` is organized as vertical slices, not by technical
role: `Auth/`, `Availability/`, `Bookings/`, `Cars/`, `Notifications/`,
`Tenants/` each hold their own service and DTOs together. A new feature
(vehicle maintenance, analytics) gets its own folder here the same way —
`Maintenance/MaintenanceService.cs` + `Maintenance/MaintenanceDtos.cs`, not a
service dropped into a shared `Services/` bucket. `Abstractions/` and
`Common/` stay reserved for things genuinely shared across every feature.

`FleetRental.Infrastructure` and `FleetRental.Api` are organized by technical
concern instead (`Controllers/`, `Persistence/Configurations/`, `Security/`)
— that's the idiomatic convention for those layers, not a smell to fix. EF
Core's `ApplyConfigurationsFromAssembly` is the reason entity configurations
live together rather than beside each feature's service.

**`FleetRental.Domain`'s entities and enums stay in `Entities/`/`Enums/`
regardless of feature, and this is deliberate, not an oversight.** EF Core's
migration snapshot (`FleetRentalDbContextModelSnapshot.cs`) identifies every
entity by its full CLR namespace as a string key. Moving `Car` from
`FleetRental.Domain.Entities` into a `FleetRental.Domain.Cars` namespace
would make EF's change-detection see that as the old entity being deleted
and a new one added — generating a migration that drops and recreates every
table. A new domain concept's entities go in `Entities/` and `Enums/`
alongside the existing ones; only `TenantEntity`/`Entity` inheritance and a
sensible class name are what identify which feature they belong to.

Backend dependencies point strictly inward:
`Api → Infrastructure → Application → Domain`.

---

## Prerequisites

- **.NET SDK 10** — `winget install Microsoft.DotNet.SDK.10`
- **Node.js 24 LTS** — `winget install OpenJS.NodeJS.LTS`
- **SQL Server 2022 Express** — `winget install Microsoft.SQLServer.2022.Express`
- **Android Studio** — only to build the Android app
- **macOS + Xcode** — only to build the iOS app (see [iOS](#ios))

---

## Running locally

### 1. Backend

```bash
cd backend && dotnet run --project src/FleetRental.Api
```

Migrations apply and demo data seeds on startup. The API listens on
`http://localhost:5180`, with Swagger at `/swagger`.

Seeded administrator: `admin@fleetrental.local` / `ChangeMe!2026`
(from `appsettings.Development.json` — change it before any deployment).

### 2. Frontend

```bash
cd frontend && npm start
```

Client app at `http://localhost:4200`, admin panel at `/admin`.

### 3. Verify

```bash
bash scripts/e2e-api-test.sh
```

19 checks covering the full flow: browse → sign up → request → approve →
calendar, plus authorization boundaries and the double-booking race.

---

## Configuration

Nothing secret is committed. `appsettings.Development.json` holds a local-only
JWT key; every other environment must supply its own or **startup fails
deliberately** rather than signing tokens with a weak key.

| Setting | Environment variable | Notes |
|---|---|---|
| `ConnectionStrings:FleetRental` | `ConnectionStrings__FleetRental` | SQL Server connection |
| `Jwt:Key` | `Jwt__Key` | 32+ characters, required |
| `Seed:AdminPassword` | `Seed__AdminPassword` | Admin seeding is skipped if unset |
| `Seed:TenantCode` | `Seed__TenantCode` | Company code for the seeded tenant (default `demo-fleet`) |
| `Platform:ProvisioningKey` | `Platform__ProvisioningKey` | Gates `POST /api/tenants`; unset closes onboarding entirely |
| `Email:*` | `Email__*` | SMTP; logs instead of sending when `Enabled` is false |
| `Cors:AllowedOrigins` | — | Must include the Capacitor origins |

```bash
dotnet user-secrets set "Jwt:Key" "<32+ character random string>" --project backend/src/FleetRental.Api
```

---

## Native builds

Rebuild the web bundle and copy it into the native shells after any frontend
change:

```bash
cd frontend && npm run build && npx cap sync
```

### Android

```bash
cd frontend && npx cap open android
```

Builds on Windows, macOS, or Linux.

**On a physical device, `localhost` is the phone, not your machine.** Point
`src/environments/environment.ts` at your LAN address (e.g.
`http://192.168.1.20:5180/api`) and make sure the API is reachable on it.

### iOS

```bash
cd frontend && npx cap open ios
```

**Requires macOS.** The Xcode project is generated and committed, so it is
ready to open — but Xcode itself is macOS-only. On Windows, use a Mac, or a
cloud macOS runner (Ionic Appflow, Codemagic, GitHub Actions `macos-latest`).
Run `pod install` in `ios/App` on first checkout.

---

## Design decisions worth knowing

**Double-booking is prevented by the database, not by application logic.**
SQL Server has no range-exclusion constraint, so an approved booking is expanded
into one `BookedDays` row per calendar day, with a unique index on
`(CarId, Date)`. Two admins approving conflicting requests at the same instant
means the second transaction rolls back. The service layer re-checks inside a
transaction to produce a readable 409, but the index is what makes the race
impossible. Both calendar features read these same rows instead of expanding
date ranges per request.

**Pending requests do not block dates.** Several clients may request overlapping
dates; only approval claims them. The calendar shows contested days in a
distinct colour.

**`Event` is its own table.** A marketing activation often needs several
vehicles — one event, several bookings. The Phase 1 UI still creates the event
inline with the booking, so clients never see the extra concept.

**Dates are whole days (`DateOnly` → SQL `date`).** A client in Dubai and an
admin in London only agree on what "the 5th" means when no clock time is
involved.

**Enums persist as strings.** Adding a category in Phase 2 will not renumber
existing rows.

---

## Built for Phase 2 and 3

- **Payments** — `Booking` already carries a decision lifecycle; add a `Payment`
  entity referencing it. `Car.DailyRate` is stored as `decimal(18,2)`.
- **WhatsApp** — implement `INotificationSender` and register it. The dispatcher
  already fans out over every registered channel; no caller changes.
- **Photo galleries** — `CarPhotos` is a full table with ordering and a primary
  flag. Phase 1 renders only the primary photo; the gallery reads the same rows.
- **Analytics** — `EventType` is a typed column, and `BookedDays` gives per-day
  utilisation without parsing ranges.
- **Multi-admin** — `Booking.DecidedByUserId` already records who decided.
  `UserRole` is a string column, so new roles do not disturb existing data.

---

## Known gaps

- **Push delivery is not wired to FCM/APNs.** Device registration, token
  storage, permission prompts, and deep-link handling are all implemented and
  working; only the final HTTP call to Firebase is stubbed
  (`PushNotificationSender.DeliverAsync`) because it needs a service-account
  key. Email notifications work today.
- **Vehicle create/edit UI** is not built. The admin API endpoints exist and are
  role-guarded; the fleet page is read-only.
- **No refresh tokens.** Access tokens last 7 days.
- **`npm audit` reports 6 moderate advisories**, all in dev-only tooling
  (`@angular/cli` and `@capacitor/cli` dependency chains). None ship in the app
  bundle. `npm audit fix --force` would *downgrade* Angular CLI to 21.0.4, so
  they are left in place deliberately.
