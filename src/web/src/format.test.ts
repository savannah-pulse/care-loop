import { describe, expect, it } from 'vitest'
import { dateText, durationText, numeric, rateText } from './format'

describe('coordination measure formatting', () => {
  it('uses elapsed time, including days and fractional seconds', () => {
    expect(durationText('1.12:30:00')).toBe('36.5 hours')
    expect(durationText('00:00:12.5000000')).toBe('12.5 seconds')
    expect(durationText('00:30:00')).toBe('30 minutes')
  })
  it.each(['24:00:00', '01:60:00', '00:00:60', '-01:00:00', 'invalid'])('refuses malformed duration %s', (value) => {
    expect(() => durationText(value)).toThrow('invalid duration')
  })
  it('keeps empty populations distinct from measured zero', () => {
    expect(rateText(null)).toBe('Not measured')
    expect(rateText(0)).toBe('0%')
    expect(rateText('0.625')).toBe('62.5%')
    expect(durationText(null)).toBe('Not measured')
  })
  it.each(['', ' ', 'bad', Infinity, NaN])('refuses malformed numeric value %s', (value) => {
    expect(() => numeric(value)).toThrow('invalid number')
  })
  it('refuses invalid timestamps and identifies the displayed time zone', () => {
    expect(() => dateText('not a timestamp')).toThrow('invalid timestamp')
    expect(dateText('2026-01-01T12:00:00Z')).toMatch(/2026.*(?:UTC|GMT|EST|CST|MST|PST|AM|PM)/)
  })
})
