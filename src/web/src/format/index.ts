/**
 * Formatting: dates and times in Abu Dhabi time on the 24-hour clock, durations in plain words,
 * the numbers the API sends as `integer | string`, and readable names for the identifiers the API
 * sends (patients, facilities and the facts a summary cites).
 */

export {
  TIME_ZONE,
  dayKey,
  describeDue,
  formatDate,
  formatDateTime,
  formatDateTimeWithYear,
  formatDayHeading,
  formatDuration,
  formatLongDateTime,
  formatTime,
  formatWhen,
  isSameDay,
  parseInstant,
  partOfDay,
} from './time'
export type { DueDescription, DurationPrecision, PartOfDay } from './time'
export { formatCount, formatPercent, parseTimeSpan, toCount, toNumber } from './numbers'
export { KNOWN_FACILITIES, factLabel, facilityName, groupDigits, humaniseId, patientLabel } from './names'
export type { KnownFacility } from './names'
