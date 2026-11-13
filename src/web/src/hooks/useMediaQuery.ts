import { useCallback, useSyncExternalStore } from 'react'

/**
 * Media query for the wide layout: tables, the ten-step stage bar and side-by-side columns.
 *
 * @remarks
 * Below 900 px, as in a side panel or on a tablet held upright, screens switch to one column with
 * cards, a compact stage bar and full-width controls. The same width is used in `index.css`.
 */
export const WIDE_LAYOUT_QUERY = '(min-width: 900px)'

/**
 * Media query for the roomiest layout: side columns on the case screen and a separate status column
 * on the worklist.
 *
 * @remarks
 * From 900 px to 1199 px the worklist table has four columns, with each case's status under the
 * patient, so long status words never squeeze or clip. The same width is used in `index.css`.
 */
export const ROOMY_LAYOUT_QUERY = '(min-width: 1200px)'

/**
 * Follows whether a CSS media query matches, such as {@link WIDE_LAYOUT_QUERY}.
 *
 * @param query - The media query, for example `(min-width: 900px)`.
 * @returns `true` while the query matches.
 *
 * @remarks
 * Side effect: listens for changes to the query, so the component re-renders when the window is
 * resized across the breakpoint. The first render already has the right answer, so nothing jumps.
 */
export function useMediaQuery(query: string): boolean {
  const subscribe = useCallback(
    (onChange: () => void) => {
      const list = window.matchMedia(query)
      list.addEventListener('change', onChange)
      return () => list.removeEventListener('change', onChange)
    },
    [query],
  )
  return useSyncExternalStore(subscribe, () => window.matchMedia(query).matches)
}
