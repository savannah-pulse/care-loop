/**
 * Shared hooks: the current time for due times, the browser tab's title, whether the wide layout
 * applies, and whether a scroll container overflows sideways.
 */

export { NOW_REFRESH_MS, useNow } from './useNow'
export { useDocumentTitle } from './useDocumentTitle'
export { ROOMY_LAYOUT_QUERY, WIDE_LAYOUT_QUERY, useMediaQuery } from './useMediaQuery'
export { useOverflowsSideways } from './useOverflowsSideways'
