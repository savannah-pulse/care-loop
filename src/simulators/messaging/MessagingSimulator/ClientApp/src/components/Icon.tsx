/** The icons the simulator draws. */
export type IconName = 'check' | 'phone' | 'left' | 'x';

/** Stroke paths of each icon, on a 24 by 24 grid. */
const PATHS: Record<IconName, string> = {
  check: 'M5 12.5l4.3 4.3L19 7.2',
  phone:
    'M6.6 3.5h3l1.6 4.2-2.2 1.5a11 11 0 0 0 5.8 5.8l1.5-2.2 4.2 1.6v3a2 2 0 0 1-2.2 2A16.5 16.5 0 0 1 4.6 5.7a2 2 0 0 1 2-2.2z',
  left: 'M14.5 6l-6 6 6 6',
  x: 'M6 6l12 12M18 6L6 18',
};

/** Props for {@link Icon}. */
export interface IconProps {
  /** Which icon to draw. */
  name: IconName;
}

/**
 * A small line icon, drawn in the current text colour.
 *
 * @remarks
 * Decorative only: it is hidden from assistive technology, so the text beside it must carry the meaning.
 */
export function Icon({ name }: IconProps) {
  return (
    <svg
      className="icon"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2.2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      <path d={PATHS[name]} />
    </svg>
  );
}
