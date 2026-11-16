import { LOCALE, t, tp } from '../i18n'

/**
 * The time zone of every date and time on CareLoop's screens: Abu Dhabi.
 *
 * @remarks
 * `Asia/Dubai` is the IANA zone for the whole of the United Arab Emirates, Abu Dhabi included. It is
 * UTC+4 all year, with no daylight saving. Staff read deadlines in local time, whatever the time
 * zone of the browser or server, so every formatter here uses this zone explicitly.
 */
export const TIME_ZONE = 'Asia/Dubai'

const MINUTE = 60_000
const MINUTES_PER_HOUR = 60
const MINUTES_PER_DAY = 24 * MINUTES_PER_HOUR

const shortParts = new Intl.DateTimeFormat(LOCALE, {
  timeZone: TIME_ZONE,
  weekday: 'short',
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
})

const longParts = new Intl.DateTimeFormat(LOCALE, {
  timeZone: TIME_ZONE,
  weekday: 'long',
  day: 'numeric',
  month: 'long',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
})

const calendarDay = new Intl.DateTimeFormat('en-CA', {
  timeZone: TIME_ZONE,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
})

interface DateParts {
  readonly weekday: string
  readonly day: string
  readonly month: string
  readonly year: string
  readonly time: string
}

function toParts(format: Intl.DateTimeFormat, date: Date): DateParts {
  const values: Partial<Record<Intl.DateTimeFormatPartTypes, string>> = {}
  for (const part of format.formatToParts(date)) {
    values[part.type] = part.value
  }
  return {
    weekday: values.weekday ?? '',
    day: values.day ?? '',
    month: values.month ?? '',
    year: values.year ?? '',
    time: `${values.hour ?? ''}:${values.minute ?? ''}`,
  }
}

/**
 * Reads a date-time the API sent as an ISO 8601 string.
 *
 * @param value - The string from the API, or nothing.
 * @returns The instant, or `undefined` when the value is missing or unreadable.
 */
export function parseInstant(value: string | null | undefined): Date | undefined {
  if (!value) {
    return undefined
  }
  const time = Date.parse(value)
  return Number.isNaN(time) ? undefined : new Date(time)
}

/**
 * Formats a time of day on the 24-hour clock, in Abu Dhabi time.
 *
 * @param date - The instant to show.
 * @returns The time, for example `09:00`.
 */
export function formatTime(date: Date): string {
  return toParts(shortParts, date).time
}

/**
 * Formats a day and time without the year, in Abu Dhabi time.
 *
 * @param date - The instant to show.
 * @returns For example `Wed 11 Nov, 15:00`.
 */
export function formatDateTime(date: Date): string {
  return t('time.dateTime', { ...toParts(shortParts, date) })
}

/**
 * Formats a day and time with the year, in Abu Dhabi time.
 *
 * @param date - The instant to show.
 * @returns For example `Tue 10 Nov 2026, 09:00`.
 */
export function formatDateTimeWithYear(date: Date): string {
  return t('time.dateTimeWithYear', { ...toParts(shortParts, date) })
}

/**
 * Formats a day and time in full, for page subtitles, in Abu Dhabi time.
 *
 * @param date - The instant to show.
 * @returns For example `Thursday 12 November 2026, 09:00`.
 */
export function formatLongDateTime(date: Date): string {
  return t('time.dateTimeWithYear', { ...toParts(longParts, date) })
}

/**
 * Formats a day without the time or year, in Abu Dhabi time.
 *
 * @param date - The instant to show.
 * @returns For example `Wed 11 Nov`.
 */
export function formatDate(date: Date): string {
  return t('time.date', { ...toParts(shortParts, date) })
}

/**
 * Formats a day with the year but without the time, in Abu Dhabi time.
 *
 * @param date - The instant to show.
 * @returns For example `Mon 3 Mar 2025`.
 */
export function formatDateWithYear(date: Date): string {
  return t('time.dateWithYear', { ...toParts(shortParts, date) })
}

/**
 * Formats a day in full without the year, as a heading over that day's events, in Abu Dhabi time.
 *
 * @param date - Any instant on the day.
 * @returns For example `Thursday 12 November`.
 */
export function formatDayHeading(date: Date): string {
  return t('time.date', { ...toParts(longParts, date) })
}

/**
 * Names the calendar day an instant falls on in Abu Dhabi, for grouping.
 *
 * @param date - The instant.
 * @returns The local day as `YYYY-MM-DD`, for example `2026-11-12`.
 */
export function dayKey(date: Date): string {
  return calendarDay.format(date)
}

/**
 * Tells whether two instants fall on the same calendar day in Abu Dhabi.
 *
 * @param a - The first instant.
 * @param b - The second instant.
 * @returns `true` when both are on the same local day.
 */
export function isSameDay(a: Date, b: Date): boolean {
  return dayKey(a) === dayKey(b)
}

/** The part of the day a greeting refers to. */
export type PartOfDay = 'morning' | 'afternoon' | 'evening'

const hourOfDay = new Intl.DateTimeFormat(LOCALE, { timeZone: TIME_ZONE, hour: '2-digit', hourCycle: 'h23' })

/**
 * Tells which part of the day it is in Abu Dhabi, for a greeting.
 *
 * @param date - The instant, usually now.
 * @returns `morning` from 05:00 to 11:59, `afternoon` from 12:00 to 17:59 and `evening` otherwise.
 */
export function partOfDay(date: Date): PartOfDay {
  const hour = Number(hourOfDay.format(date))
  if (hour >= 5 && hour < 12) {
    return 'morning'
  }
  return hour >= 12 && hour < 18 ? 'afternoon' : 'evening'
}

/**
 * Formats a day and time relative to now: "Today" for the current day, the date otherwise.
 *
 * @param date - The instant to show.
 * @param now - The current time.
 * @returns For example `Today, 12:00` or `Fri 13 Nov, 17:00`.
 */
export function formatWhen(date: Date, now: Date): string {
  return isSameDay(date, now) ? t('time.today', { time: formatTime(date) }) : formatDateTime(date)
}

/**
 * How finely {@link formatDuration} describes a duration of a day or more.
 *
 * @remarks
 * `coarse` gives whole days, which is enough for a deadline ("1 day left"). `fine` adds the hours,
 * for measures such as a median time to closure.
 */
export type DurationPrecision = 'coarse' | 'fine'

/**
 * Formats a duration in plain words.
 *
 * @param milliseconds - The duration; the sign is ignored.
 * @param precision - Whether durations of a day or more also show their hours.
 * @returns For example `40 min`, `1 h 40 min`, `18 h`, `2 days` or `19 days 6 h`.
 */
export function formatDuration(milliseconds: number, precision: DurationPrecision = 'coarse'): string {
  const totalMinutes = Math.floor(Math.abs(milliseconds) / MINUTE)
  if (totalMinutes < MINUTES_PER_HOUR) {
    return t('time.minutes', { count: Math.max(totalMinutes, 1) })
  }
  if (totalMinutes < MINUTES_PER_DAY) {
    const hours = Math.floor(totalMinutes / MINUTES_PER_HOUR)
    const minutes = totalMinutes % MINUTES_PER_HOUR
    return minutes === 0 ? t('time.hours', { count: hours }) : t('time.hoursMinutes', { hours, minutes })
  }
  const days = Math.floor(totalMinutes / MINUTES_PER_DAY)
  const hours = Math.floor((totalMinutes % MINUTES_PER_DAY) / MINUTES_PER_HOUR)
  return precision === 'fine' && hours > 0 ? tp('time.daysHours', days, { hours }) : tp('time.days', days)
}

const SECOND = 1000

/**
 * Formats a configured duration exactly, such as a programme's deadline or reminder time.
 *
 * @param milliseconds - The duration; the sign is ignored.
 * @returns The largest unit with the next one when it is not zero, without rounding the largest
 * unit away: for example `1 day`, `1 day 6 h`, `4 h`, `2 h 30 min`, `30 min`, `1 min 30 s` or
 * `48 s`. Settings can be seconds long, as in a shortened pathway for a live run.
 */
export function formatSpan(milliseconds: number): string {
  const totalSeconds = Math.round(Math.abs(milliseconds) / SECOND)
  const days = Math.floor(totalSeconds / 86_400)
  const hours = Math.floor((totalSeconds % 86_400) / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)
  const seconds = totalSeconds % 60
  if (days > 0) {
    return hours > 0 ? tp('time.daysHours', days, { hours }) : tp('time.days', days)
  }
  if (hours > 0) {
    return minutes > 0 ? t('time.hoursMinutes', { hours, minutes }) : t('time.hours', { count: hours })
  }
  if (minutes > 0) {
    return seconds > 0 ? t('time.minutesSeconds', { minutes, seconds }) : t('time.minutes', { count: minutes })
  }
  return t('time.seconds', { count: seconds })
}

/** A due time described for a person: the main line, the line under it and whether it has passed. */
export interface DueDescription {
  /** `true` when the due time has passed. */
  readonly overdue: boolean
  /** The main line: how late, or when it is due. */
  readonly primary: string
  /** The line under it: when it was due, or how much time is left. */
  readonly secondary: string
}

/**
 * Describes a due time against the current time.
 *
 * @param due - When the action is due.
 * @param now - The current time.
 * @returns `18 h late` over `Was due Wed 11 Nov, 15:00` when it has passed, or `Today, 12:00` over
 * `3 h left` when it has not.
 */
export function describeDue(due: Date, now: Date): DueDescription {
  const remaining = due.getTime() - now.getTime()
  if (Math.abs(remaining) < MINUTE) {
    return { overdue: false, primary: formatWhen(due, now), secondary: t('time.dueNow') }
  }
  if (remaining < 0) {
    return {
      overdue: true,
      primary: t('time.late', { duration: formatDuration(remaining) }),
      secondary: t('time.wasDue', { date: formatWhen(due, now) }),
    }
  }
  return {
    overdue: false,
    primary: formatWhen(due, now),
    secondary: t('time.left', { duration: formatDuration(remaining) }),
  }
}
