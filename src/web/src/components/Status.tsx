import type { ReactNode } from 'react'
import type { Tone } from '../domain'

/** Props for {@link StatusText}. */
export interface StatusTextProps {
  /** The colour of the dot. */
  readonly tone: Tone
  /** The status word, for example `Overdue`. */
  readonly children: ReactNode
}

/**
 * A status written as a word with a coloured dot in front, as in tables and lists.
 *
 * @remarks
 * The word always carries the meaning; the dot only repeats it, so status is never shown by colour
 * alone.
 */
export function StatusText({ tone, children }: StatusTextProps) {
  return <span className={`st ${tone}`}>{children}</span>
}

/** Props for {@link Badge}. */
export interface BadgeProps {
  /** The colour of the badge, and of its dot for every tone except grey. */
  readonly tone: Tone
  /** The badge text. */
  readonly children: ReactNode
}

/**
 * A small pill holding a status or a label, as on cards and tracks.
 *
 * @remarks
 * Coloured badges carry a dot and a word; grey badges are labels without a dot.
 */
export function Badge({ tone, children }: BadgeProps) {
  return <span className={`badge ${tone}`}>{children}</span>
}
