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

---

## Open

_(none)_
