/**
 * Language layer: every user-visible string comes from a catalogue in `src/locales`.
 *
 * @remarks
 * English only for now. Catalogues are keyed by language so Arabic can be added later by adding
 * `locales/ar.json` and listing it in {@link SUPPORTED_LANGUAGES}; no component hard-codes text.
 * The layer is deliberately small (lookup, `{name}` placeholders and plural forms) so it needs no
 * dependency.
 */

import en from '../locales/en.json'

/** Languages the app has a catalogue for. */
export const SUPPORTED_LANGUAGES = ['en'] as const

/** A language code the app has a catalogue for. */
export type Language = (typeof SUPPORTED_LANGUAGES)[number]

/** The language the app shows. */
export const LANGUAGE: Language = 'en'

/**
 * BCP 47 locale used to format dates and numbers.
 *
 * @remarks
 * British English gives day-month order and the 24-hour clock that staff screens use.
 */
export const LOCALE = 'en-GB'

/** The shape every catalogue must have: the English one. */
type Catalogue = typeof en

const catalogues: Record<Language, Catalogue> = { en }

type Join<P extends string, K extends string> = P extends '' ? K : `${P}.${K}`

type StringPaths<T, P extends string = ''> = {
  [K in keyof T & string]: T[K] extends string ? Join<P, K> : StringPaths<T[K], Join<P, K>>
}[keyof T & string]

type PluralPaths<T, P extends string = ''> = {
  [K in keyof T & string]: T[K] extends string
    ? never
    : T[K] extends { one: string; other: string }
      ? Join<P, K>
      : PluralPaths<T[K], Join<P, K>>
}[keyof T & string]

/** Dot-separated path to a single message in the catalogue, for example `worklist.title`. */
export type MessageKey = StringPaths<Catalogue>

/** Dot-separated path to a message with plural forms (`one` and `other`). */
export type PluralKey = PluralPaths<Catalogue>

/** Values substituted for `{name}` placeholders in a message. */
export type MessageParams = Readonly<Record<string, string | number>>

const pluralRules = new Intl.PluralRules(LOCALE)
const numberFormat = new Intl.NumberFormat(LOCALE)

function lookup(path: string): unknown {
  let node: unknown = catalogues[LANGUAGE]
  for (const part of path.split('.')) {
    if (node === null || typeof node !== 'object' || !(part in node)) {
      return undefined
    }
    node = (node as Record<string, unknown>)[part]
  }
  return node
}

function interpolate(template: string, params?: MessageParams): string {
  if (!params) {
    return template
  }
  return template.replace(/\{(\w+)\}/g, (placeholder: string, name: string) => {
    const value = params[name]
    if (value === undefined) {
      return placeholder
    }
    return typeof value === 'number' ? numberFormat.format(value) : value
  })
}

/**
 * Returns a message in the current language.
 *
 * @param key - Path to the message in the catalogue.
 * @param params - Values for the message's `{name}` placeholders; numbers are formatted for the locale.
 * @returns The message, or the key itself when the catalogue has no such string.
 *
 * @example
 * ```ts
 * t('time.wasDue', { date: 'Wed 11 Nov, 15:00' }) // "Was due Wed 11 Nov, 15:00"
 * ```
 */
export function t(key: MessageKey, params?: MessageParams): string {
  const template = lookup(key)
  return typeof template === 'string' ? interpolate(template, params) : key
}

/**
 * Returns the message whose plural form matches a count.
 *
 * @param key - Path to a message with `one` and `other` forms.
 * @param count - The number the message is about; it fills the `{count}` placeholder.
 * @param params - Values for any other placeholders.
 * @returns The message in the form the locale's plural rules choose for `count`.
 *
 * @example
 * ```ts
 * tp('worklist.summary.open', 1) // "1 open case"
 * tp('worklist.summary.open', 8) // "8 open cases"
 * ```
 */
export function tp(key: PluralKey, count: number, params?: MessageParams): string {
  const forms = lookup(key) as Partial<Record<Intl.LDMLPluralRule, string>> | undefined
  const template = forms?.[pluralRules.select(count)] ?? forms?.other
  return template === undefined ? key : interpolate(template, { ...params, count })
}

/**
 * Tells whether the catalogue has a single message at a path built at run time.
 *
 * @param key - A path that may or may not exist, for example one built from a value the API returned.
 * @returns `true`, narrowing `key` to {@link MessageKey}, when a string exists at that path.
 */
export function hasMessage(key: string): key is MessageKey {
  return typeof lookup(key) === 'string'
}
