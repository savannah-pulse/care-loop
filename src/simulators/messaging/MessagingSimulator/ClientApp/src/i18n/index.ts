import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';

import en from '../locales/en.json';

/**
 * Languages the UI has a catalogue for.
 *
 * @remarks
 * English only for now. Catalogues are keyed by language so Arabic (`ar`) can be added by
 * dropping `locales/ar.json` in and listing it here; no component hard-codes text.
 */
export const SUPPORTED_LANGUAGES = ['en'] as const;

/** A language code the UI has a catalogue for. */
export type SupportedLanguage = (typeof SUPPORTED_LANGUAGES)[number];

/** The language used when none is selected or a key is missing. */
export const DEFAULT_LANGUAGE: SupportedLanguage = 'en';

/** Translation catalogues, keyed by language. */
const resources = {
  en: { translation: en },
} satisfies Record<SupportedLanguage, { translation: typeof en }>;

void i18n.use(initReactI18next).init({
  resources,
  lng: DEFAULT_LANGUAGE,
  fallbackLng: DEFAULT_LANGUAGE,
  supportedLngs: [...SUPPORTED_LANGUAGES],
  interpolation: { escapeValue: false },
  initAsync: false,
});

/**
 * The initialised i18next instance used by the whole ClientApp.
 *
 * @remarks
 * Importing this module initialises i18next synchronously with the bundled catalogues, so
 * components can call `useTranslation()` on first render.
 */
export default i18n;
