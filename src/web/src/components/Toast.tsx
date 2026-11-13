import { useEffect } from 'react'
import { t } from '../i18n'
import { Icon } from './Icon'

/** How long a toast stays before it goes by itself, in milliseconds. */
export const TOAST_DURATION_MS = 6000

/** Props for {@link Toast}. */
export interface ToastProps {
  /** The message to show; nothing shows when absent. */
  readonly message?: string
  /** Called when the toast goes, by itself after {@link TOAST_DURATION_MS} or when dismissed. */
  readonly onDismiss: () => void
}

/**
 * A calm, short confirmation at the bottom of the screen, such as "Summary approved."
 *
 * @remarks
 * The live region is always on the page, so screen readers announce each new message politely.
 * The message goes by itself after a few seconds or when dismissed; what it confirms is also shown
 * on the screen, so nothing is lost when it goes.
 */
export function Toast({ message, onDismiss }: ToastProps) {
  useEffect(() => {
    if (!message) {
      return undefined
    }
    const timer = window.setTimeout(onDismiss, TOAST_DURATION_MS)
    return () => window.clearTimeout(timer)
  }, [message, onDismiss])

  return (
    <div className="toast-region" role="status" aria-live="polite">
      {message ? (
        <div className="toast">
          <Icon name="check" />
          <span className="toast-text">{message}</span>
          <button type="button" className="toast-close" aria-label={t('states.dismiss')} onClick={onDismiss}>
            <Icon name="x" />
          </button>
        </div>
      ) : null}
    </div>
  )
}
