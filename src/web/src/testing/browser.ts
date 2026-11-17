/**
 * What the screen tests need from a browser that jsdom does not do: media queries, modal dialogs and
 * scrolling.
 *
 * @remarks
 * Each filler behaves like the browsers the dashboard supports, as far as the screens can tell, so
 * a test exercises the screen's own code: `matchMedia` answers width queries for the test window's
 * width, a `dialog` opened with `showModal` shows, takes focus and closes on Escape, and scrolling
 * does nothing.
 */

/** The test window's width when a test does not choose one: 1024 px, jsdom's own, the wide layout. */
export const DEFAULT_SCREEN_WIDTH = 1024

const mediaLists = new Map<string, TestMediaQueryList>()

function matchesWidth(query: string): boolean {
  const width = window.innerWidth
  const parts = query.split(/\s+and\s+/i)
  return parts.every((part) => {
    const min = /min-width:\s*(\d+)px/i.exec(part)
    if (min) {
      return width >= Number(min[1])
    }
    const max = /max-width:\s*(\d+)px/i.exec(part)
    return max ? width <= Number(max[1]) : false
  })
}

class TestMediaQueryList extends EventTarget {
  readonly media: string
  onchange: ((event: Event) => void) | null = null
  #matched: boolean

  constructor(media: string) {
    super()
    this.media = media
    this.#matched = matchesWidth(media)
  }

  get matches(): boolean {
    return matchesWidth(this.media)
  }

  addListener(listener: (event: Event) => void): void {
    this.addEventListener('change', listener)
  }

  removeListener(listener: (event: Event) => void): void {
    this.removeEventListener('change', listener)
  }

  widthChanged(): void {
    if (this.matches !== this.#matched) {
      this.#matched = this.matches
      const event = new Event('change')
      this.onchange?.(event)
      this.dispatchEvent(event)
    }
  }
}

/**
 * Sets the test window's width, as if the window were resized, for the screen's media queries.
 *
 * @param width - The width in CSS pixels, for example 640 for a side panel.
 */
export function setScreenWidth(width: number): void {
  Object.defineProperty(window, 'innerWidth', { value: width, configurable: true, writable: true })
  for (const list of mediaLists.values()) {
    list.widthChanged()
  }
}

function matchMedia(query: string): MediaQueryList {
  let list = mediaLists.get(query)
  if (list === undefined) {
    list = new TestMediaQueryList(query)
    mediaLists.set(query, list)
  }
  return list as unknown as MediaQueryList
}

const FOCUSABLE = 'button, input, select, textarea, a[href], [tabindex]:not([tabindex="-1"])'

function fillDialogs(): void {
  const dialog = HTMLDialogElement.prototype
  if (typeof dialog.showModal === 'function') {
    return
  }
  dialog.show = function show(this: HTMLDialogElement): void {
    this.open = true
  }
  dialog.showModal = function showModal(this: HTMLDialogElement): void {
    this.open = true
    const target = this.querySelector<HTMLElement>('[autofocus]') ?? this.querySelector<HTMLElement>(FOCUSABLE)
    target?.focus()
  }
  dialog.close = function close(this: HTMLDialogElement): void {
    if (!this.open) {
      return
    }
    this.open = false
    this.dispatchEvent(new Event('close'))
  }
  document.addEventListener('keydown', (event) => {
    const open = [...document.querySelectorAll('dialog[open]')].at(-1)
    if (event.key === 'Escape' && open instanceof HTMLDialogElement) {
      if (open.dispatchEvent(new Event('cancel', { cancelable: true }))) {
        open.close()
      }
    }
  })
}

/**
 * Fills what jsdom lacks for the screens: `matchMedia` for width queries, modal dialogs, and
 * scrolling.
 *
 * @remarks
 * The test setup calls it once per test file. A media query other than `min-width` or `max-width`,
 * such as a preference for reduced motion, never matches.
 */
export function fillBrowserGaps(): void {
  window.matchMedia = matchMedia
  window.scrollTo = () => undefined
  if (typeof Element.prototype.scrollIntoView !== 'function') {
    Element.prototype.scrollIntoView = () => undefined
  }
  fillDialogs()
}
