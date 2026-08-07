import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export type AppLocale = 'en' | 'ar';

const LOCALE_KEY = 'fleet_rental_locale';
const RTL_LOCALES: ReadonlySet<AppLocale> = new Set(['ar']);

/** A nested translation tree, e.g. { cars: { title: "Our Fleet" } }. */
type TranslationTree = { [key: string]: string | TranslationTree };

/**
 * Runtime language switching (English/Arabic) with RTL.
 */
@Injectable({ providedIn: 'root' })
export class LocaleStore {
  private readonly http = inject(HttpClient);

  /**
   * Caches translations already fetched, so switching back to a language
   * already used this session is instant and does not re-request the file.
   */
  private readonly cache = new Map<AppLocale, TranslationTree>();

  private readonly _locale = signal<AppLocale>('en');
  private readonly _translations = signal<TranslationTree>({});
  private readonly _ready = signal(false);

  readonly locale = this._locale.asReadonly();
  readonly ready = this._ready.asReadonly();
  readonly isRtl = computed(() => RTL_LOCALES.has(this._locale()));

  /**
   * The BCP-47 tag used for date formatting. The `-u-nu-latn` extension keeps
   * Western digits in Arabic — Gulf business apps read dates with Arabic month
   * names but Western numerals, and the browser's Arabic locale data defaults
   * to Arabic-Indic digits without it.
   */
  readonly intlLocale = computed(() => (this._locale() === 'ar' ? 'ar-u-nu-latn' : 'en-US'));

  /**
   * Loads the remembered language (or defaults to English) and applies it to
   * the document before the app renders its first frame. Called once from the
   * root component, ahead of tenant and session restoration, so no screen ever
   * flashes in the wrong language or direction.
   */
  async init(): Promise<void> {
    const stored = localStorage.getItem(LOCALE_KEY);
    const initial: AppLocale = stored === 'ar' ? 'ar' : 'en';
    await this.setLocale(initial);
  }

  async setLocale(locale: AppLocale): Promise<void> {
    if (!this.cache.has(locale)) {
      const translations = await firstValueFrom(
        this.http.get<TranslationTree>(`/i18n/${locale}.json`),
      );
      this.cache.set(locale, translations);
    }

    this._translations.set(this.cache.get(locale)!);
    this._locale.set(locale);
    this._ready.set(true);
    localStorage.setItem(LOCALE_KEY, locale);
    this.applyToDocument(locale);
  }

  toggle(): Promise<void> {
    return this.setLocale(this._locale() === 'en' ? 'ar' : 'en');
  }

  /**
   * Looks up a dot-path key (e.g. "cars.seats") and substitutes any
   * {{placeholder}} tokens. Missing keys return the key itself rather than an
   * empty string, so a translation gap is visible in the UI instead of silent.
   */
  t(key: string, params?: Record<string, string | number>): string {
    const value = this.resolve(key, this._translations());

    if (typeof value !== 'string') {
      return key;
    }

    if (!params) {
      return value;
    }

    return Object.entries(params).reduce(
      (text, [name, replacement]) => text.replaceAll(`{{${name}}}`, String(replacement)),
      value,
    );
  }

  /**
   * Formats an ISO date (date-only "2026-10-05" or a full timestamp) in the
   * current language, via the native Intl API rather than Angular's DatePipe —
   * DatePipe needs `@angular/common/locales/ar` registered against LOCALE_ID to
   * format Arabic, which is a build-time, single-locale mechanism unsuited to a
   * runtime language switch. Intl needs no registration for any locale.
   */
  formatDate(iso: string, options: Intl.DateTimeFormatOptions): string {
    // A bare "YYYY-MM-DD" is parsed as UTC midnight by the Date constructor;
    // appending a local time avoids the date shifting a day for any timezone
    // west of UTC.
    const date = iso.length === 10 ? new Date(`${iso}T00:00:00`) : new Date(iso);
    return date.toLocaleDateString(this.intlLocale(), options);
  }

  private resolve(key: string, tree: TranslationTree): string | undefined {
    const value = key.split('.').reduce<string | TranslationTree | undefined>(
      (node, segment) => (typeof node === 'object' && node !== null ? node[segment] : undefined),
      tree,
    );

    return typeof value === 'string' ? value : undefined;
  }

  /**
   * Sets lang/dir on <html>. Ionic's own components read `dir` to mirror
   * automatically (paddings, icons, slide direction); this is the one place
   * that has to run for every language change, or the framework and the
   * document disagree about which way the UI faces.
   */
  private applyToDocument(locale: AppLocale): void {
    document.documentElement.lang = locale;
    document.documentElement.dir = RTL_LOCALES.has(locale) ? 'rtl' : 'ltr';
  }
}
