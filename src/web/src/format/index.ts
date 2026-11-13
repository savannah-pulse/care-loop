/**
 * Formatting: dates and times in Abu Dhabi time on the 24-hour clock, durations in plain words,
 * and the numbers the API sends as `integer | string`.
 */

export {
  TIME_ZONE,
  describeDue,
  formatDateTime,
  formatDateTimeWithYear,
  formatDuration,
  formatLongDateTime,
  formatTime,
  formatWhen,
  isSameDay,
  parseInstant,
} from './time'
export type { DueDescription, DurationPrecision } from './time'
export { formatCount, formatPercent, parseTimeSpan, toCount, toNumber } from './numbers'
