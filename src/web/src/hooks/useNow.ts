import { useEffect, useState } from 'react'

/** How often {@link useNow} refreshes by default: once a minute, the finest unit due times show. */
export const NOW_REFRESH_MS = 60_000

/**
 * Follows the current time, so "3 h left" and "18 h late" stay true while a screen is open.
 *
 * @param refreshMs - How often to refresh, in milliseconds.
 * @returns The current time, updated on each refresh.
 *
 * @remarks
 * Side effect: runs a timer while the component is mounted. CareLoop always runs on real time.
 */
export function useNow(refreshMs: number = NOW_REFRESH_MS): Date {
  const [now, setNow] = useState(() => new Date())
  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), refreshMs)
    return () => window.clearInterval(timer)
  }, [refreshMs])
  return now
}
