import i18n from './i18n';

/**
 * Formats a timestamp from the simulator's host for the screen.
 *
 * @param iso - ISO 8601 time, or `null` when there is none.
 * @returns The time in the UI language's usual form, or an empty string when there is none or it cannot be read.
 */
export function formatTime(iso: string | null): string {
  if (!iso) {
    return '';
  }

  const date = new Date(iso);
  return Number.isNaN(date.getTime())
    ? ''
    : date.toLocaleString(i18n.language, { dateStyle: 'medium', timeStyle: 'medium' });
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
 * @returns Hours and minutes in the UI language's usual form, or an empty string when there is none or it cannot be read.
 */
export function formatClock(iso: string | null): string {
  if (!iso) {
    return '';
  }

  const date = new Date(iso);
  return Number.isNaN(date.getTime())
    ? ''
    : date.toLocaleTimeString(i18n.language, { hour: 'numeric', minute: '2-digit' });
}
