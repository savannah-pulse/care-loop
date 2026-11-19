import { useId, useLayoutEffect, useRef, type ReactNode, type SyntheticEvent } from 'react'

/** Props for {@link Dialog}. */
export interface DialogProps {
  /** Whether the dialog is open. */
  readonly open: boolean
  /** The dialog's heading, which also names it for screen readers. */
  readonly title: string
  /** Called when the person closes the dialog with Escape; the parent then sets `open` to `false`. */
  readonly onClose: () => void
  /** The dialog's content: its message, any field and its buttons. */
  readonly children: ReactNode
}

/**
 * A small modal dialog for a decision that needs a confirmation or a reason, such as
 * acknowledging an escalation.
 *
 * @remarks
 * Built on the native `dialog` element opened as a modal, so focus moves into it and stays there,
 * the page behind it cannot be used, and Escape closes it. When it closes, focus goes back to the
 * control that opened it if that control is still on the page. The dialog opens and closes in a
 * layout effect, before other effects run, so a screen can move focus once the dialog has closed.
 */
export function Dialog({ open, title, onClose, children }: DialogProps) {
  const ref = useRef<HTMLDialogElement>(null)
  const opener = useRef<HTMLElement | null>(null)
  const titleId = useId()

  useLayoutEffect(() => {
    const dialog = ref.current
    if (!dialog) {
      return
    }
    if (open && !dialog.open) {
      opener.current = document.activeElement instanceof HTMLElement ? document.activeElement : null
      dialog.showModal()
    } else if (!open && dialog.open) {
      dialog.close()
      const back = opener.current
      opener.current = null
      if (back?.isConnected) {
        back.focus()
      }
    }
  }, [open])

  const onCancel = (event: SyntheticEvent<HTMLDialogElement>): void => {
    event.preventDefault()
    onClose()
  }

  return (
    <dialog ref={ref} className="dialog" aria-labelledby={titleId} onCancel={onCancel}>
      <h2 className="dialog-title" id={titleId}>
        {title}
      </h2>
      {children}
    </dialog>
  )
}
