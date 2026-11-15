import en from './locales/en.json'

/** All UI text keys in the current English catalogue. */
export type TextKey = keyof typeof en

/**
 * Looks up English UI text and interpolates named values.
 * @param key - A catalogue key.
 * @param values - Named interpolation values.
 * @returns Localized text.
 */
export function t(key: TextKey, values: Record<string, string | number> = {}): string {
  return en[key].replace(/\{(\w+)\}/g, (_, name: string) => String(values[name] ?? `{${name}}`))
}

/**
 * Translates known coordination codes; pathway-defined values remain source configuration.
 * @param group - The catalogue group.
 * @param code - The source code, if present.
 * @returns A localized label, or the original unrecognized source code.
 */
export function codeText(group: string, code: string | null | undefined): string {
  if (code == null) return t('common.none')
  const key = `${group}.${code}`
  return key in en ? t(key as TextKey) : code
}
