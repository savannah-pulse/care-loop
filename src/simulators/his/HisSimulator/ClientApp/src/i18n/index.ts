import i18next from 'i18next';
import { initReactI18next } from 'react-i18next';

import en from '../locales/en.json';

/**
 * The language used when no other is available.
 *
 * @remarks
 * English is the only language for now. Everything is language-keyed so Arabic (and a
 * right-to-left layout) can be added later by registering another resource bundle.
 */
export const DEFAULT_LANGUAGE = 'en';

/**
 * The i18next instance every component translates through.
 *
 * @remarks
 * Initialised synchronously from the bundled `locales/en.json`, so no component ever renders
 * with missing strings. All user-visible text must come from here; nothing is hard-coded.
 */
export const i18n = i18next.createInstance();

void i18n.use(initReactI18next).init({
  lng: DEFAULT_LANGUAGE,
  fallbackLng: DEFAULT_LANGUAGE,
  resources: { en: { translation: en } },
  interpolation: { escapeValue: false },
  initAsync: false,
});
