import { t } from './i18n'

/**
 * Formats a source timestamp, showing the user's time zone explicitly.
 * @param value - An ISO timestamp.
 * @returns A localized date/time or a missing-value label.
 * @throws Error - The API supplied an invalid timestamp.
 */
export function dateText(value: string | null | undefined): string {
  if (value == null) return t('common.none')
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) throw new Error(t('error.invalidDate'))
  return new Intl.DateTimeFormat('en', {
    year: 'numeric', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit', timeZoneName: 'short',
  }).format(date)
}

/**
 * Formats an API TimeSpan as elapsed hours, minutes or seconds, without a working-hours calendar.
 * @param value - A .NET constant-format duration.
 * @returns An elapsed-time label, or an unmeasured label.
 * @throws Error - The duration does not match the API's TimeSpan format.
 */
export function durationText(value: string | null | undefined): string {
  if (value == null) return t('common.notMeasured')
  const match = /^(?:(\d+)\.)?(\d{2}):(\d{2}):(\d{2})(?:\.(\d{1,7}))?$/.exec(value)
  if (!match || Number(match[2]) > 23 || Number(match[3]) > 59 || Number(match[4]) > 59)
    throw new Error(t('error.invalidDuration'))
  const hours = Number(match[1] ?? 0) * 24 + Number(match[2]) + Number(match[3]) / 60
    + (Number(match[4]) + Number(`0.${match[5] ?? 0}`)) / 3600
  const format = new Intl.NumberFormat('en', { maximumFractionDigits: 2 })
  return hours >= 1 ? t('common.hours', { hours: format.format(hours) })
    : hours >= 1 / 60 ? t('common.minutes', { minutes: format.format(hours * 60) })
      : t('common.seconds', { seconds: format.format(hours * 3600) })
}

/**
 * Formats a coordination rate without turning an empty population into zero percent.
 * @param value - A numeric or numeric-string rate from zero to one, or absent.
 * @returns A percentage or an unmeasured label.
 * @throws Error - The API supplied a malformed numeric rate.
 */
export function rateText(value: number | string | null | undefined): string {
  return value == null ? t('common.notMeasured') : new Intl.NumberFormat('en', { style: 'percent', maximumFractionDigits: 1 }).format(numeric(value))
}

/**
 * Reads the number-or-numeric-string shape supported by the OpenAPI contract.
 * @param value - The API numeric value.
 * @returns A finite number.
 * @throws Error - The API supplied a malformed number.
 */
export function numeric(value: number | string): number {
  const parsed = typeof value === 'number' ? value : Number(value)
  if (!Number.isFinite(parsed) || typeof value === 'string' && value.trim() === '') throw new Error(t('error.invalidNumber'))
  return parsed
}
