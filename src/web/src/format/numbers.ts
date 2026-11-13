import { LOCALE } from '../i18n'

/**
 * Reads a count the API sent as a JSON number or a numeric string.
 *
 * @remarks
 * The API is .NET and allows numbers to be written as strings, so the OpenAPI document types
 * every count as `integer | string`. Convert at the edge so the screens only see numbers.
 *
 * @param value - The value from the API.
 * @returns The number, or `undefined` when the value is missing or not a number.
 *
 * @example
 * ```ts
 * toNumber('240') // 240
 * toNumber(null) // undefined
 * ```
 */
export function toNumber(value: number | string | null | undefined): number | undefined {
  if (value === null || value === undefined || value === '') {
    return undefined
  }
  const parsed = Number(value)
  return Number.isFinite(parsed) ? parsed : undefined
}

/**
 * Reads a count, treating anything missing or unreadable as zero.
 *
 * @param value - The value from the API.
 * @returns The number, or `0`.
 */
export function toCount(value: number | string | null | undefined): number {
  return toNumber(value) ?? 0
}

const integerFormat = new Intl.NumberFormat(LOCALE, { maximumFractionDigits: 0 })
const percentFormat = new Intl.NumberFormat(LOCALE, { style: 'percent', maximumFractionDigits: 0 })

/**
 * Formats a whole number for the current locale.
 *
 * @param value - The number to show.
 * @returns The number with locale grouping, for example `1,240`.
 */
export function formatCount(value: number): string {
  return integerFormat.format(value)
}

/**
 * Formats a share from 0 to 1 as a whole percentage.
 *
 * @param rate - The share, for example `0.77`.
 * @returns The percentage, for example `77%`.
 */
export function formatPercent(rate: number): string {
  return percentFormat.format(rate)
}

const timeSpanPattern = /^(-)?(?:(\d+)\.)?(\d{1,2}):(\d{2}):(\d{2})(?:\.(\d{1,7}))?$/

/**
 * Reads a .NET `TimeSpan` such as `1.02:30:00` (one day, two hours and thirty minutes).
 *
 * @param value - The duration as the API writes it: `[-][d.]hh:mm:ss[.fffffff]`.
 * @returns The duration in milliseconds, or `undefined` when the value is missing or not a duration.
 */
export function parseTimeSpan(value: string | null | undefined): number | undefined {
  const match = value ? timeSpanPattern.exec(value) : null
  if (!match) {
    return undefined
  }
  const [, sign, days, hours, minutes, seconds, fraction] = match
  const milliseconds =
    ((Number(days ?? 0) * 24 + Number(hours)) * 60 + Number(minutes)) * 60_000 +
    Number(seconds) * 1000 +
    Math.round(Number(`0.${fraction ?? '0'}`) * 1000)
  return sign ? -milliseconds : milliseconds
}
