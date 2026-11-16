import type { ReactNode } from 'react'
import type { Tone } from '../domain'

/** Props for {@link Badge}. */
export interface BadgeProps {
  /** The colour of the badge, and of its dot for every tone except grey. */
  readonly tone: Tone
  /** The badge text. */
  readonly children: ReactNode
}

/**
 * A soft pill holding a status or a label, as on cases, cards and tracks.
 *
 * @remarks
 * Coloured pills carry a dot and a word on a tinted background with dark text, so status is never
 * shown by colour alone; grey pills are labels without a dot.
 */
export function Badge({ tone, children }: BadgeProps) {
  return <span className={`badge ${tone}`}>{children}</span>
}
