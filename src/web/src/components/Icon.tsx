import type { ReactNode } from 'react'

/** Names of the icons the app draws. */
export type IconName =
  | 'alert'
  | 'check'
  | 'clock'
  | 'doc'
  | 'down'
  | 'left'
  | 'lock'
  | 'search'
  | 'spark'
  | 'user'
  | 'x'

interface IconShape {
  readonly strokeWidth?: number
  readonly filled?: boolean
  readonly body: ReactNode
}

const ICONS: Readonly<Record<IconName, IconShape>> = {
  alert: {
    strokeWidth: 2,
    body: (
      <>
        <path d="M12 3.8l9 15.7H3z" />
        <path d="M12 10v4.2M12 16.9v.2" />
      </>
    ),
  },
  check: { strokeWidth: 2.6, body: <path d="M5 12.5l4.3 4.3L19 7.2" /> },
  clock: {
    strokeWidth: 2,
    body: (
      <>
        <circle cx="12" cy="12" r="8.5" />
        <path d="M12 7.5V12l3 2" />
      </>
    ),
  },
  doc: {
    strokeWidth: 1.9,
    body: (
      <>
        <path d="M7 3.5h7l4 4v13H7z" />
        <path d="M14 3.5v4h4M9.5 12h5M9.5 15.5h5" />
      </>
    ),
  },
  down: { strokeWidth: 2, body: <path d="M7 10l5 5 5-5" /> },
  left: { strokeWidth: 2.2, body: <path d="M14.5 6l-6 6 6 6" /> },
  lock: {
    strokeWidth: 1.9,
    body: (
      <>
        <rect x="5" y="10.5" width="14" height="10" rx="2" />
        <path d="M8.5 10.5V8a3.5 3.5 0 0 1 7 0v2.5" />
      </>
    ),
  },
  search: {
    strokeWidth: 2,
    body: (
      <>
        <circle cx="11" cy="11" r="6.5" />
        <path d="M16 16l4 4" />
      </>
    ),
  },
  spark: { filled: true, body: <path d="M12 3l2 5.6 5.6 2-5.6 2L12 18.2l-2-5.6-5.6-2 5.6-2z" /> },
  user: {
    strokeWidth: 2,
    body: (
      <>
        <circle cx="12" cy="8.5" r="3.8" />
        <path d="M4.8 20a7.2 7.2 0 0 1 14.4 0" />
      </>
    ),
  },
  x: { strokeWidth: 2.6, body: <path d="M7 7l10 10M17 7L7 17" /> },
}

/** Props for {@link Icon}. */
export interface IconProps {
  /** Which icon to draw. */
  readonly name: IconName
  /** Extra class names, for example to size the icon. */
  readonly className?: string
}

/**
 * A small line icon from the CareLoop design.
 *
 * @remarks
 * Icons are decoration only: they are hidden from assistive technology, so the text next to them
 * must say the same thing. Status is never shown by an icon or colour alone.
 */
export function Icon({ name, className }: IconProps) {
  const shape = ICONS[name]
  return (
    <svg
      className={className ? `i ${className}` : 'i'}
      viewBox="0 0 24 24"
      aria-hidden="true"
      focusable="false"
      fill={shape.filled ? 'currentColor' : 'none'}
      stroke={shape.filled ? 'none' : 'currentColor'}
      strokeWidth={shape.strokeWidth}
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      {shape.body}
    </svg>
  )
}

/**
 * The CareLoop mark: an open loop in the accent colour, shown next to the product name.
 *
 * @remarks
 * Decorative; the product name is always written next to it.
 */
export function Logo() {
  return (
    <svg className="logo" viewBox="0 0 28 28" aria-hidden="true" focusable="false">
      <circle
        cx="14"
        cy="14"
        r="10"
        fill="none"
        stroke="currentColor"
        strokeWidth="3.4"
        strokeLinecap="round"
        strokeDasharray="50 13"
        transform="rotate(-60 14 14)"
      />
      <circle cx="19" cy="5.4" r="2.7" fill="currentColor" />
    </svg>
  )
}
