/** Rows shown on one page of a case list. */
export const PAGE_SIZE = 50

/**
 * Splits a page that was requested one row over its size.
 * @param fetched - Rows returned for a request of `PAGE_SIZE + 1` rows.
 * @returns The rows to show and whether a following page exists.
 * @remarks Asking for one extra row is what tells a page that ends exactly on the page size from one that has
 * more behind it, so the Next button never leads to an empty page.
 */
export function splitPage<T>(fetched: readonly T[]): { rows: T[]; hasMore: boolean } {
  return { rows: fetched.slice(0, PAGE_SIZE), hasMore: fetched.length > PAGE_SIZE }
}
