import type { ReactNode } from 'react'

/** Names of the icons the app draws. */
export type IconName =
  | 'alert'
  | 'calendar'
  | 'chat'
  | 'check'
  | 'clock'
  | 'doc'
  | 'down'
  | 'left'
  | 'list'
  | 'lock'
  | 'phone'
  | 'right'
  | 'search'
  | 'send'
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
  calendar: {
    strokeWidth: 1.9,
    body: (
      <>
        <rect x="4" y="5.5" width="16" height="14.5" rx="2" />
        <path d="M4 10h16M8.5 3.5v4M15.5 3.5v4" />
      </>
    ),
  },
  chat: {
    strokeWidth: 1.9,
    body: (
      <>
        <path d="M5 5h14a1.5 1.5 0 0 1 1.5 1.5v8.5a1.5 1.5 0 0 1-1.5 1.5h-8.5l-4.5 3.5v-3.5H5A1.5 1.5 0 0 1 3.5 15V6.5A1.5 1.5 0 0 1 5 5z" />
        <path d="M8 9.5h8M8 12.5h5" />
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
  list: {
    strokeWidth: 1.9,
    body: (
      <>
        <path d="M9.5 7h10M9.5 12h10M9.5 17h10" />
        <path d="M4.8 7h.4M4.8 12h.4M4.8 17h.4" strokeWidth="2.6" />
      </>
    ),
  },
  lock: {
    strokeWidth: 1.9,
    body: (
      <>
        <rect x="5" y="10.5" width="14" height="10" rx="2" />
        <path d="M8.5 10.5V8a3.5 3.5 0 0 1 7 0v2.5" />
      </>
    ),
  },
  phone: {
    strokeWidth: 1.9,
    body: (
      <path d="M8.4 4.2l1.3 3.2a1.2 1.2 0 0 1-.3 1.3L8 10a11.6 11.6 0 0 0 6 6l1.3-1.4a1.2 1.2 0 0 1 1.3-.3l3.2 1.3a1.2 1.2 0 0 1 .7 1.3l-.4 2a1.8 1.8 0 0 1-1.9 1.4A15.6 15.6 0 0 1 3.7 5.7 1.8 1.8 0 0 1 5.1 3.8l2-.4a1.2 1.2 0 0 1 1.3.8z" />
    ),
  },
  right: { strokeWidth: 2.2, body: <path d="M9.5 6l6 6-6 6" /> },
  search: {
    strokeWidth: 2,
    body: (
      <>
        <circle cx="11" cy="11" r="6.5" />
        <path d="M16 16l4 4" />
      </>
    ),
  },
  send: {
    strokeWidth: 1.9,
    body: (
      <>
        <path d="M20.5 3.5L3.5 10.4l6.8 3.3 3.3 6.8z" />
        <path d="M20.5 3.5l-10.2 10.2" />
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
