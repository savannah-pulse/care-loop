import { useEffect, useState, type RefObject } from 'react'

/**
 * Tells whether an element's content is wider than the element, so it scrolls sideways.
 *
 * @param ref - The element, usually a scroll container around a table.
 * @returns `true` while the content overflows sideways.
 *
 * @remarks
 * Side effect: watches the element and its children with a `ResizeObserver`. A container that
 * scrolls must be reachable by keyboard (WCAG 2.1.1), so screens make it focusable only while this
 * is `true`, and leave out the extra tab stop when everything fits.
 */
export function useOverflowsSideways(ref: RefObject<HTMLElement | null>): boolean {
  const [overflows, setOverflows] = useState(false)
  useEffect(() => {
    const element = ref.current
    if (!element) {
      return undefined
    }
    const measure = (): void => setOverflows(element.scrollWidth > element.clientWidth + 1)
    measure()
    const observer = new ResizeObserver(measure)
    observer.observe(element)
    for (const child of element.children) {
      observer.observe(child)
    }
    return () => observer.disconnect()
  }, [ref])
  return overflows
}
