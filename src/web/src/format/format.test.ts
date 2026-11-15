import { describe, expect, it } from 'vitest'
import {
  TIME_ZONE,
  describeDue,
  formatDateTimeWithYear,
  formatRate,
  formatSpan,
  formatTime,
  measuredDuration,
  parseInstant,
  parseTimeSpan,
  toCount,
  toNumber,
} from '.'

const MINUTE = 60_000
const HOUR = 60 * MINUTE
const DAY = 24 * HOUR

describe('durations from the API', () => {
  it('reads .NET TimeSpan values, including days and fractional seconds', () => {
    expect(parseTimeSpan('1.12:30:00')).toBe(DAY + 12 * HOUR + 30 * MINUTE)
    expect(parseTimeSpan('1.00:00:00')).toBe(DAY)
    expect(parseTimeSpan('08:00:00')).toBe(8 * HOUR)
    expect(parseTimeSpan('00:30:00')).toBe(30 * MINUTE)
    expect(parseTimeSpan('00:00:12.5000000')).toBe(12_500)
    expect(parseTimeSpan('-01:00:00')).toBe(-HOUR)
  })

  it('reads ISO 8601 durations as well', () => {
    expect(parseTimeSpan('PT8H')).toBe(8 * HOUR)
    expect(parseTimeSpan('P1DT12H30M')).toBe(DAY + 12 * HOUR + 30 * MINUTE)
    expect(parseTimeSpan('PT0.5S')).toBe(500)
    expect(parseTimeSpan('P1W')).toBe(7 * DAY)
    expect(parseTimeSpan('-PT1H')).toBe(-HOUR)
  })

  it.each(['24:00:00', '01:60:00', '00:00:60', '1:00', 'invalid', '', '   ', 'P', 'PT', null, undefined])(
    'refuses malformed duration %s',
    (value) => {
      expect(parseTimeSpan(value)).toBeUndefined()
    },
  )

  it('never treats a negative or missing duration as a measure', () => {
    expect(measuredDuration('-01:00:00')).toBeUndefined()
    expect(measuredDuration(null)).toBeUndefined()
    expect(measuredDuration('8.00:50:00')).toBe(8 * DAY + 50 * MINUTE)
  })

  it('words configured durations exactly, down to seconds', () => {
    expect(formatSpan(DAY)).toBe('1 day')
    expect(formatSpan(DAY + 6 * HOUR)).toBe('1 day 6 h')
    expect(formatSpan(2 * DAY)).toBe('2 days')
    expect(formatSpan(4 * HOUR)).toBe('4 h')
    expect(formatSpan(2 * HOUR + 30 * MINUTE)).toBe('2 h 30 min')
    expect(formatSpan(90_000)).toBe('1 min 30 s')
    expect(formatSpan(48_000)).toBe('48 s')
  })
})

describe('numbers from the API', () => {
  it.each(['', ' ', 'bad', Infinity, NaN, null, undefined])('refuses malformed numeric value %s', (value) => {
    expect(toNumber(value)).toBeUndefined()
  })

  it('reads numbers sent as JSON numbers or numeric strings', () => {
    expect(toNumber('240')).toBe(240)
    expect(toNumber(0)).toBe(0)
    expect(toNumber('0.625')).toBe(0.625)
    expect(toCount('bad')).toBe(0)
  })

  it('keeps an empty population apart from a measured zero', () => {
    expect(formatRate(null)).toBeUndefined()
    expect(formatRate(undefined)).toBeUndefined()
    expect(formatRate(0)).toBe('0%')
    expect(formatRate('0.625')).toBe('63%')
    expect(formatRate(1)).toBe('100%')
  })
})

describe('times on screen', () => {
  it('refuses unreadable timestamps', () => {
    expect(parseInstant('not a timestamp')).toBeUndefined()
    expect(parseInstant(null)).toBeUndefined()
    expect(parseInstant('')).toBeUndefined()
  })

  it('shows every time in Abu Dhabi, whatever the time zone of the computer', () => {
    const instant = new Date('2026-01-01T12:00:00Z')
    expect(TIME_ZONE).toBe('Asia/Dubai')
    expect(formatTime(instant)).toBe('16:00')
    expect(formatDateTimeWithYear(instant)).toBe('Thu 1 Jan 2026, 16:00')
  })

  it('words how late a step is, or how long is left', () => {
    const now = new Date('2026-11-12T05:00:00Z')
    expect(describeDue(new Date('2026-11-11T11:00:00Z'), now)).toEqual({
      overdue: true,
      primary: '18 h late',
      secondary: 'Was due Wed 11 Nov, 15:00',
    })
    expect(describeDue(new Date('2026-11-12T08:00:00Z'), now)).toEqual({
      overdue: false,
      primary: 'Today, 12:00',
      secondary: '3 h left',
    })
  })
})
