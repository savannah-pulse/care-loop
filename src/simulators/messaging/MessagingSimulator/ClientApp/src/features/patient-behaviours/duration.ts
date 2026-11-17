/**
 * Writes a number of seconds as a .NET duration, the form the simulator's host reads.
 *
 * @param seconds - A delay in seconds, zero or more; fractions are kept to the millisecond.
 * @returns The duration as `[d.]hh:mm:ss[.fff]`, for example `00:00:02` or `1.01:00:00`.
 */
export function toDotNetDuration(seconds: number): string {
  const totalMs = Math.max(0, Math.round(seconds * 1000));
  const ms = totalMs % 1000;
  const totalSeconds = Math.floor(totalMs / 1000);
  const days = Math.floor(totalSeconds / 86400);
  const two = (value: number) => String(value).padStart(2, '0');
  const clock = [
    Math.floor((totalSeconds % 86400) / 3600),
    Math.floor((totalSeconds % 3600) / 60),
    totalSeconds % 60,
  ]
    .map(two)
    .join(':');

  return `${days > 0 ? `${days}.` : ''}${clock}${ms > 0 ? `.${String(ms).padStart(3, '0')}` : ''}`;
}

/**
 * Reads a .NET duration as a number of seconds.
 *
 * @param duration - A duration as the simulator's host writes it, for example `00:00:02` or `1.01:00:00.5000000`.
 * @returns The duration in seconds, or 0 when the text is not a duration.
 */
export function fromDotNetDuration(duration: string): number {
  const match = /^(-)?(?:(\d+)\.)?(\d{1,2}):(\d{2}):(\d{2})(?:\.(\d{1,7}))?$/.exec(duration.trim());
  if (!match) {
    return 0;
  }

  const [, negative, days, hours, minutes, seconds, fraction] = match;
  const total =
    Number(days ?? 0) * 86400 +
    Number(hours) * 3600 +
    Number(minutes) * 60 +
    Number(seconds) +
    (fraction ? Number(`0.${fraction}`) : 0);

  return negative ? -total : total;
}
