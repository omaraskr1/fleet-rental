# Bugs

Tracked issues found during QA/UAT passes. Update status in place when a bug is fixed — don't delete history, just flip the status and add a note.

Status legend: `OPEN` · `IN PROGRESS` · `FIXED` · `WON'T FIX`

---

## Fixed

### BUG-001: Booking submission gets permanently stuck disabled
**Status:** FIXED (2026-08-08)
**Found:** 2026-08-08
**Component:** `frontend/src/app/features/bookings/booking-form.page.ts`
**Severity:** P1 — Critical

**Summary:** The "Submit request" button never enabled when the user followed the exact flow the UI itself prescribes ("1. Pick your dates" → "2. Tell us about the event"), blocking the core booking flow for essentially all real users.

**Root cause:** `isValid` was declared as an Angular `computed()` signal that read `startDate()`/`endDate()` (real signals) plus `eventName`/`location` (plain class fields bound via `[(ngModel)]`, not signals). `computed()` only re-evaluates when a *signal* dependency changes, so the memoized result got fixed the moment dates were picked (while the text fields were still empty) and never recomputed afterward no matter what the user typed.

**Fix:** Converted `isValid` from a `computed()` signal into a plain method, so it re-evaluates on every change-detection cycle instead of being memoized against only the date signals. No template changes needed — `isValid()` was already called as a function.

**Verification:**
- `ng test` — 104/104 passed, no regressions
- Live browser repro of the exact original bug order (pick dates → fill Event name/Location, no re-touching the calendar) confirmed via `isValid()` returning `true` and the Submit button's `disabled` property reading `false`
- Full booking submitted end-to-end and appeared correctly in the admin approval queue

---

### BUG-002: Admin panel light theme doesn't fully apply
**Status:** FIXED (2026-08-08)
**Found:** 2026-08-08
**Component:** `frontend/src/app/features/admin/admin-shell.page.ts`
**Severity:** P2 — Medium

**Summary:** Selecting "Light" theme while the OS/browser preferred dark correctly flipped `ion-content` areas to white, but the admin sidebar stayed dark — a jarring two-tone white-dashboard-next-to-black-sidebar look.

**Root cause:** Angular components default to `display: inline`; `admin-shell.page.ts` had no `:host` style, so the host element never reported a real height. `.layout`'s `min-height: 100%` then resolved against effectively nothing, and the shorter column (`<aside>`, just nav links) left unpainted browser canvas showing through below its content — which renders dark/black when the OS prefers dark, regardless of the app's own light-theme choice. The taller `main` column didn't show the gap because its card content filled the available height.

**Fix:** Added `:host { display: block; min-height: 100%; }` plus explicit `background: var(--ion-background-color)` (and `color: var(--ion-text-color)`) on `.layout` and `<aside>`, so the correct theme color always paints instead of relying on inherited/transparent background reaching all the way down to raw canvas.

**Verification:**
- `ng test` — 104/104 passed, no regressions
- Logged in as admin, toggled theme to "Light" against a dark-preferring browser, confirmed via screenshot that sidebar and content are now uniformly white

---

### BUG-003: Placeholder text low contrast, easily mistaken for real input
**Status:** FIXED (2026-08-08)
**Found:** 2026-08-08
**Component:** `frontend/src/styles.scss` (global `ion-input`/`ion-textarea` placeholder styling)
**Severity:** P3 — Low

**Summary:** Placeholder text (e.g. "Autumn Product Launch", "250") rendered at ~60% opacity on the dark theme — visually close enough to real entered text that a tester could believe a required field was already filled and submit with it blank. (This is what led to discovering BUG-001 — it initially looked like fields were pre-filled.)

**Fix:** Added a global rule dropping placeholder opacity to 0.45 and marking placeholders italic (both `!important`, since Ionic's own internal `::placeholder` rule otherwise wins the cascade) so filled vs. empty is unambiguous at a glance.

**Verification:**
- `ng test` — 104/104 passed, no regressions
- Confirmed via computed style on a live booking-form input: `opacity: 0.45`, `font-style: italic`

### BUG-004: Client tab pages (Fleet/Bookings/Profile) don't respond to real clicks or taps
**Status:** FIXED (2026-08-08)
**Found:** 2026-08-08
**Component:** `frontend/src/app/features/tabs/tabs.page.ts`
**Severity:** P0 — Blocker

**Summary:** Inside the client-facing tabbed shell, nothing rendered by the active tab page responded to a real mouse click or touch tap — not car cards, not category filter chips, not buttons, not the sign-out button on Profile. The only thing that reliably responded to a click was the tab bar itself (Fleet / Bookings / Profile). This made the primary client app close to unusable: you could switch tabs, but not interact with anything a tab showed you.

**Root cause:** `TabsPage`'s template declared its own `<ion-router-outlet />` as a direct child of `<ion-tabs>`. But `IonTabs` (the Angular wrapper component from `@ionic/angular`, confirmed by reading `node_modules/@ionic/angular/esm2022/directives/navigation/ion-tabs.mjs`) **already generates its own internal `<ion-router-outlet>`** inside a `.tabs-inner` wrapper whenever it has no `<ion-tab>` children — which is exactly this app's setup (tab content is routed via the Angular Router, not `<ion-tab>`). The app's manually-added outlet was therefore redundant: it got projected into `IonTabs`' trailing catch-all `<ng-content>` slot, landing as an empty, full-viewport-sized, `pointer-events: auto` sibling sitting on top of the real (correctly positioned, Ionic-managed) outlet inside `.tabs-inner`. Confirmed via `document.elementFromPoint()` at the exact screen position of visible, rendered content (car cards, filter chips, the sign-out button): it resolved to the empty `<ion-router-outlet><!----></ion-router-outlet>`, not the actual element being looked at. Matches the known upstream Ionic issue category — nested/managed `ion-router-outlet` inside `ion-tabs` — e.g. [ionic-team/ionic-framework#21748](https://github.com/ionic-team/ionic-framework/issues/21748) and [ionic-team/ionic#20219](https://github.com/ionic-team/ionic/issues/20219).

**Fix:** Removed the app's manually-authored `<ion-router-outlet />` (and the now-unused `IonRouterOutlet` import) from `tabs.page.ts`. `IonTabs` provides the outlet on its own — no other change was needed, since Angular Router was already resolving to whichever outlet actually got registered for the tab content.

**Verification:**
- `ng test` — 104/104 passed, no regressions
- Rebuilt and redeployed the Docker test stack; confirmed via real (non-JS-dispatched) clicks in the browser that car cards navigate to detail, and the sign-out button on Profile works and correctly clears the session
- `document.elementFromPoint()` at car-card and profile-button coordinates now resolves to the actual element, not a phantom outlet

**Note:** this was also the root cause of the separately-reported "client user cannot sign out" issue — same phantom-outlet layer was swallowing clicks on the Profile tab's sign-out button. Fixed by the same change; verified above.

---

## Feature change (not a bug)

### Fleet/car data now requires a signed-in account
**Status:** DONE (2026-08-08)
**Requested:** 2026-08-08, by the user, alongside the BUG-004 fix

**Summary:** Previously, browsing the fleet (car list, car detail, availability) was intentionally anonymous — clients could look before creating an account. Per the user's explicit request ("no user should see any data until he has login in the system"), this is now gated behind authentication end to end:

- **Backend** (`CarsController.cs`): `GetAll`, `GetById`, `GetAvailability`, and `CheckAvailability` changed from `[AllowAnonymous]` to `[Authorize]` (any signed-in role, not just Admin).
- **Frontend** (`app.routes.ts`): added `authGuard` to the `cars` tab route and the top-level `cars/:id` (car detail) route. `cars/:id/book` already had it.

This is a real behavior change, not a bug fix — anonymous pre-signup browsing was a deliberate original design choice (see the git history / old doc comments), now deliberately reversed.

**Test changes required:** several backend integration tests exercised anonymous car browsing as part of testing *other* things (mainly tenant isolation's fail-closed behavior). Updated to authenticate first where the test's actual point was unaffected by the auth change (e.g. `A_tenants_cars_are_invisible_to_another_tenant`, `A_cars_availability_is_not_readable_from_another_tenant`, the `Availability_reflects_exactly_the_approved_days` double-booking test), or redirected through `/api/auth/login` (which remains anonymous/tenant-header-scoped) where the test specifically needed to exercise tenant resolution without cars being available as a surface for that anymore (`An_unknown_company_code_yields_nothing_rather_than_everything`, `A_request_with_no_company_code_sees_nothing`, `A_suspended_tenant_is_treated_as_unknown` — these now assert `400 BadRequest` on login, since "no tenant resolved" fails before credential-checking, which is a different, earlier failure than the `401` used for "wrong credentials in a resolved tenant"). `Browsing_cars_does_not_require_an_account` renamed to `Browsing_cars_requires_an_account` and rewritten to assert the opposite.

**Verification:**
- `dotnet test` — 273/273 passed (177 unit + 96 integration)
- `ng test` — 104/104 passed
- Live browser: selecting a company now lands directly on Sign In with zero fleet data visible; only after signup/login does `/tabs/cars` show anything

---

## Open

_(none)_
