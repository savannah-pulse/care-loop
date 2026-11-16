import { LOCALE } from '../i18n'

/**
 * Reads a count the API sent as a JSON number or a numeric string.
 *
 * @remarks
 * The API is .NET and allows numbers to be written as strings, so the OpenAPI document types
 * every count as `integer | string`. Convert at the edge so the screens only see numbers.
 *
 * @param value - The value from the API.
 * @returns The number, or `undefined` when the value is missing, blank, not a number or not finite,
 * so a malformed value is never shown as a made-up number such as zero.
 *
 * @example
 * ```ts
 * toNumber('240') // 240
 * toNumber(' ') // undefined
 * toNumber(null) // undefined
 * ```
 */
export function toNumber(value: number | string | null | undefined): number | undefined {
  if (value === null || value === undefined || (typeof value === 'string' && value.trim() === '')) {
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
const isoDurationPattern =
  /^(-)?P(?:(\d+(?:[.,]\d+)?)W)?(?:(\d+(?:[.,]\d+)?)D)?(?:T(?:(\d+(?:[.,]\d+)?)H)?(?:(\d+(?:[.,]\d+)?)M)?(?:(\d+(?:[.,]\d+)?)S)?)?$/i

function isoNumber(value: string | undefined): number {
  return value === undefined ? 0 : Number(value.replace(',', '.'))
}

function parseIsoDuration(value: string): number | undefined {
  const match = isoDurationPattern.exec(value)
  if (!match || value.toUpperCase().endsWith('T') || /^-?P$/i.test(value)) {
    return undefined
  }
  const [, sign, weeks, days, hours, minutes, seconds] = match
  const milliseconds = Math.round(
    (((isoNumber(weeks) * 7 + isoNumber(days)) * 24 + isoNumber(hours)) * 60 + isoNumber(minutes)) * 60_000 +
      isoNumber(seconds) * 1000,
  )
  return sign ? -milliseconds : milliseconds
}

/**
 * Reads a duration from the API: a .NET `TimeSpan` such as `1.02:30:00` (one day, two hours and
 * thirty minutes) or an ISO 8601 duration such as `P1DT2H30M`.
 *
 * @param value - The duration as the API writes it: `[-][d.]hh:mm:ss[.fffffff]`, or ISO 8601.
 * @returns The duration in milliseconds, or `undefined` when the value is missing or not a duration,
 * including a `TimeSpan` with hours past 23 or minutes and seconds past 59.
 *
 * @example
 * ```ts
 * parseTimeSpan('08:00:00') // 28800000
 * parseTimeSpan('00:00:12.5000000') // 12500
 * parseTimeSpan('PT8H') // 28800000
 * ```
 */
export function parseTimeSpan(value: string | null | undefined): number | undefined {
  const text = value?.trim() ?? ''
  if (text === '') {
    return undefined
  }
  if (/^-?P/i.test(text)) {
    return parseIsoDuration(text)
  }
  const match = timeSpanPattern.exec(text)
  if (!match) {
    return undefined
  }
  const [, sign, days, hours, minutes, seconds, fraction] = match
  if (Number(hours) > 23 || Number(minutes) > 59 || Number(seconds) > 59) {
    return undefined
  }
  const milliseconds =
    ((Number(days ?? 0) * 24 + Number(hours)) * 60 + Number(minutes)) * 60_000 +
    Number(seconds) * 1000 +
    Math.round(Number(`0.${fraction ?? '0'}`) * 1000)
  return sign ? -milliseconds : milliseconds
}

/**
 * Reads a measured duration, such as a median time to a step.
 *
 * @param value - The duration as the API writes it, or `null` when nothing was measured.
 * @returns The duration in milliseconds, or `undefined` when nothing was measured or the value is
 * not a duration. A negative duration is not a measure, so it is `undefined` too.
 */
export function measuredDuration(value: string | null | undefined): number | undefined {
  const milliseconds = parseTimeSpan(value)
  return milliseconds === undefined || milliseconds < 0 ? undefined : milliseconds
}

/**
 * Formats a rate from the API as a whole percentage.
 *
 * @param rate - The rate from 0 to 1, as a number or numeric string; `null` when nobody was eligible.
 * @returns The percentage, for example `86%`, or `undefined` when there is no rate, so an empty
 * population is never shown as 0%.
 */
export function formatRate(rate: number | string | null | undefined): string | undefined {
  const value = toNumber(rate)
  return value === undefined ? undefined : formatPercent(value)
}

const decimalFormat = new Intl.NumberFormat(LOCALE, { minimumFractionDigits: 1, maximumFractionDigits: 1 })

/**
 * Formats a number with one decimal place, such as an average rating.
 *
 * @param value - The number.
 * @returns For example `4.4`.
 */
export function formatDecimal(value: number): string {
  return decimalFormat.format(value)
}
