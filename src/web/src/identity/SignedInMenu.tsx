import { useEffect, useId, useRef, useState } from 'react'
import { Icon } from '../components/Icon'
import { t } from '../i18n'
import { STAFF, staffDescription } from './staff'
import { signInAs, useSignedInStaff } from './store'

/**
 * The "Signed in as" menu at the end of the header: who is acting, and a list to switch person.
 *
 * @remarks
 * Prototype identity scheme, to be replaced by real sign-in. Switching person changes the identity
 * headers sent with every API call, and each screen loads its data again. The list closes on
 * Escape, on a click outside it or once a person is chosen.
 */
export function SignedInMenu() {
  const me = useSignedInStaff()
  const [open, setOpen] = useState(false)
  const panelId = useId()
  const containerRef = useRef<HTMLDivElement>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    if (!open) {
      return undefined
    }
    const onKeyDown = (event: KeyboardEvent): void => {
      if (event.key === 'Escape') {
        setOpen(false)
        buttonRef.current?.focus()
      }
    }
    const onPointerDown = (event: PointerEvent): void => {
      if (!containerRef.current?.contains(event.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('keydown', onKeyDown)
    document.addEventListener('pointerdown', onPointerDown)
    return () => {
      document.removeEventListener('keydown', onKeyDown)
      document.removeEventListener('pointerdown', onPointerDown)
    }
  }, [open])

  const choose = (id: string): void => {
    signInAs(id)
    setOpen(false)
    buttonRef.current?.focus()
  }

  return (
    <div className="me-menu" ref={containerRef}>
      <button
        ref={buttonRef}
        type="button"
        className="me"
        aria-expanded={open}
        aria-controls={panelId}
        onClick={() => setOpen((value) => !value)}
      >
        <span className="av" aria-hidden="true">
          {me.initials}
        </span>
        <span className="me-text">
          <span className="visually-hidden">{t('identity.signedInAs')} </span>
          <b>{me.name}</b>
          <small>{staffDescription(me)}</small>
        </span>
        <Icon name="down" className="me-caret" />
      </button>
      <div id={panelId} className="me-panel" hidden={!open}>
        <p className="me-panel-title" id={`${panelId}-title`}>
          {t('identity.signedInAs')}
        </p>
        <ul aria-labelledby={`${panelId}-title`}>
          {STAFF.map((person) => {
            const current = person.id === me.id
            return (
              <li key={person.id}>
                <button
                  type="button"
                  className="me-option"
                  aria-current={current ? 'true' : undefined}
                  onClick={() => choose(person.id)}
                >
                  <span className="av" aria-hidden="true">
                    {person.initials}
                  </span>
                  <span className="me-text">
                    <b>{person.name}</b>
                    <small>{staffDescription(person)}</small>
                  </span>
                  {current ? (
                    <span className="me-current">
                      <Icon name="check" />
                      {t('identity.current')}
                    </span>
                  ) : null}
                </button>
              </li>
            )
          })}
        </ul>
      </div>
    </div>
  )
}
