import { useId, useRef, useState, type FormEvent, type ReactNode } from 'react'
import { t } from '../i18n'

/** Props for {@link AccessReasonForm}. */
export interface AccessReasonFormProps {
  /** What the person is about to open, and that their reason is recorded. */
  readonly intro: string
  /** Fields shown before the reasons, such as which team to look at. */
  readonly before?: ReactNode
  /** The reasons to choose from; "Another reason, which I will write" is always added last. */
  readonly options: readonly string[]
  /** The words on the button that opens what was asked for, for example `Open the case`. */
  readonly submitLabel: string
  /** Called with the chosen or written reason, without surrounding spaces. */
  readonly onSubmit: (reason: string) => void
  /** Called when the person decides not to open it. */
  readonly onCancel: () => void
}

const OTHER = 'other'

/**
 * Asks why someone needs a case or a list outside their team, before it opens (board 14).
 *
 * @remarks
 * The API refuses reads outside the person's facility unless they carry a reason, and records the
 * reason with every read. Nothing is chosen in advance, so the reason is always a deliberate
 * choice; "Another reason" asks for the reason in the person's own words. The reason is kept in
 * memory only, for the reads that need it.
 */
export function AccessReasonForm({ intro, before, options, submitLabel, onSubmit, onCancel }: AccessReasonFormProps) {
  const ids = useId()
  const [choice, setChoice] = useState('')
  const [written, setWritten] = useState('')
  const [problem, setProblem] = useState<'choose' | 'write'>()
  const firstRef = useRef<HTMLInputElement>(null)
  const writtenRef = useRef<HTMLTextAreaElement>(null)

  const submit = (event: FormEvent<HTMLFormElement>): void => {
    event.preventDefault()
    if (choice === '') {
      setProblem('choose')
      firstRef.current?.focus()
      return
    }
    if (choice === OTHER) {
      if (written.trim() === '') {
        setProblem('write')
        writtenRef.current?.focus()
        return
      }
      onSubmit(written.trim())
      return
    }
    onSubmit(choice)
  }

  return (
    <form className="reason-form" onSubmit={submit} noValidate>
      <p className="reason-intro">{intro}</p>
      {before}
      <fieldset aria-describedby={problem === 'choose' ? `${ids}-choose` : undefined}>
        <legend>{t('access.legend')}</legend>
        {[...options, OTHER].map((option, index) => (
          <label key={option} className="radio">
            <input
              ref={index === 0 ? firstRef : undefined}
              type="radio"
              name={`${ids}-reason`}
              value={option}
              checked={choice === option}
              onChange={() => {
                setChoice(option)
                setProblem(undefined)
              }}
            />
            <span>{option === OTHER ? t('access.other') : option}</span>
          </label>
        ))}
      </fieldset>
      {problem === 'choose' ? (
        <p className="field-error" id={`${ids}-choose`} role="alert">
          {t('access.chooseReason')}
        </p>
      ) : null}
      {choice === OTHER ? (
        <div className="reason-written">
          <label htmlFor={`${ids}-written`}>{t('access.writtenLabel')}</label>
          <textarea
            ref={writtenRef}
            id={`${ids}-written`}
            className="textarea"
            value={written}
            maxLength={1000}
            aria-invalid={problem === 'write'}
            aria-describedby={problem === 'write' ? `${ids}-write` : undefined}
            onChange={(event) => {
              setWritten(event.target.value)
              setProblem(undefined)
            }}
          />
          {problem === 'write' ? (
            <p className="field-error" id={`${ids}-write`} role="alert">
              {t('access.writeReason')}
            </p>
          ) : null}
        </div>
      ) : null}
      <div className="dialog-actions">
        <button type="button" className="btn" onClick={onCancel}>
          {t('access.cancel')}
        </button>
        <button type="submit" className="btn primary">
          {submitLabel}
        </button>
      </div>
    </form>
  )
}
