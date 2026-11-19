/**
 * Formatting: dates and times in Abu Dhabi time on the 24-hour clock, durations in plain words,
 * the numbers the API sends as `integer | string`, and readable names for the identifiers the API
 * sends (patients and facilities).
 */

export {
  TIME_ZONE,
  dayKey,
  describeDue,
  formatDate,
  formatDateTime,
  formatDateTimeWithYear,
  formatDateWithYear,
  formatDayHeading,
  formatDuration,
  formatLongDateTime,
  formatSpan,
  formatTime,
  formatWhen,
  isSameDay,
  parseInstant,
  partOfDay,
} from './time'
export type { DueDescription, DurationPrecision, PartOfDay } from './time'
export {
  formatCount,
  formatDecimal,
  formatPercent,
  formatRate,
  measuredDuration,
  parseTimeSpan,
  toCount,
  toNumber,
} from './numbers'
export { KNOWN_FACILITIES, facilityName, groupDigits, humaniseId, patientLabel } from './names'
export type { KnownFacility } from './names'
