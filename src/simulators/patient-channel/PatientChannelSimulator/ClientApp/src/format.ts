/**
 * The time zone the simulator shows times in: Abu Dhabi, as the CareLoop dashboard does, so the two screens agree.
 */
export const TIME_ZONE = 'Asia/Dubai';

const dateTime = new Intl.DateTimeFormat('en-GB', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
  timeZone: TIME_ZONE,
});

const clock = new Intl.DateTimeFormat('en-GB', {
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
  timeZone: TIME_ZONE,
});

/**
 * Formats a timestamp from the simulator's host for the screen.
 *
 * @param iso - ISO 8601 time, or `null` when there is none.
 * @returns The day and 24-hour time in Abu Dhabi, for example `17 Nov 2026, 00:11`, as the dashboard writes it; an
 * empty string when there is none or it cannot be read.
 */
export function formatTime(iso: string | null): string {
  if (!iso) {
    return '';
  }

  const date = new Date(iso);
  return Number.isNaN(date.getTime()) ? '' : dateTime.format(date);
}

/**
 * Pretty-prints a JSON body for reading.
 *
 * @param body - The text exchanged, or `null` when there was none.
 * @returns Indented JSON when the text is JSON, otherwise the text unchanged; an empty string when there was none.
 */
export function formatJson(body: string | null): string {
  if (!body) {
    return '';
  }

  try {
    return JSON.stringify(JSON.parse(body), null, 2);
  } catch {
    return body;
  }
}

/**
 * Formats a timestamp as a clock time, the way a phone's status bar shows it.
 *
 * @param iso - ISO 8601 time, or `null` when there is none.
 * @returns Hours and minutes on the 24-hour clock in Abu Dhabi, for example `00:11`, or an empty string when there is none or it cannot be read.
 */
export function formatClock(iso: string | null): string {
  if (!iso) {
    return '';
  }

  const date = new Date(iso);
  return Number.isNaN(date.getTime()) ? '' : clock.format(date);
}
